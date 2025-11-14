using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Biblioteka.Controllers
{
    // Dostęp tylko dla Pracownika/Administratora
    [Authorize(Roles = "worker,admin")]
    public class RezerwacjaController : Controller
    {
        private readonly BibliotekaContext _context;

        public RezerwacjaController(BibliotekaContext context)
        {
            _context = context;
        }

        // AKCJA 1: Wyświetla listę aktywnych rezerwacji (kolejka)
        [HttpGet]
        public async Task<IActionResult> Index(string? searchString, bool? readyForPickup) // DODANO parametry filtrowania
        {
            // 1. Budowanie zapytania bazowego
            var rezerwacjeQuery = _context.Rezerwacje
                .Where(r => r.IsActive)
                .Include(r => r.User)
                .Include(r => r.Ksiazka)
                .AsQueryable();

            // 2. Filtrowanie po gotowości do odbioru
            if (readyForPickup.HasValue && readyForPickup.Value)
            {
                rezerwacjeQuery = rezerwacjeQuery.Where(r => r.Ksiazka!.stan == "Gotowa do Odbioru");
            }

            // 3. Wyszukiwanie tekstowe
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                rezerwacjeQuery = rezerwacjeQuery.Where(r =>
                    r.Ksiazka!.tytul.ToLower().Contains(searchString) ||
                    r.User!.Nazwisko.ToLower().Contains(searchString));
            }

            // 4. Sortowanie (Gotowe do odbioru na górze)
            var aktywneRezerwacje = await rezerwacjeQuery
                .OrderByDescending(r => r.Ksiazka!.stan == "Gotowa do Odbioru")
                .ThenBy(r => r.KsiazkaId)
                .ThenBy(r => r.DataRezerwacji)
                .ToListAsync();

            ViewBag.CurrentSearch = searchString;
            ViewBag.IsReady = readyForPickup;

            return View(aktywneRezerwacje);
        }

        // AKCJA 2: Anulowanie rezerwacji
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var rezerwacja = await _context.Rezerwacje.FindAsync(id);

            if (rezerwacja == null || !rezerwacja.IsActive)
            {
                TempData["Message"] = "Błąd: Aktywna rezerwacja nie została znaleziona.";
                return RedirectToAction(nameof(Index));
            }

            // Dezaktywacja rezerwacji
            rezerwacja.IsActive = false;
            _context.Rezerwacje.Update(rezerwacja);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Pomyślnie anulowano rezerwację dla {rezerwacja.Ksiazka?.tytul}.";
            return RedirectToAction(nameof(Index));
        }

        // AKCJA 4: Finalizacja Odbioru (Wypożyczenie Zarezerwowanej Książki)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizePickup(int id) // ID Rezerwacji
        {
            var rezerwacja = await _context.Rezerwacje
                .Include(r => r.Ksiazka)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (rezerwacja == null || rezerwacja.Ksiazka?.stan != "Gotowa do Odbioru")
            {
                TempData["Message"] = "Błąd: Rezerwacja jest nieaktywna lub książka nie jest gotowa do odbioru.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tworzenie nowego rekordu wypożyczenia dla rezerwującego
                var noweWypozyczenie = new Wypozyczenie
                {
                    UserId = rezerwacja.UserId,
                    KsiazkaId = rezerwacja.KsiazkaId,
                    DataWypozyczenia = DateTime.Now,
                    OczekiwanaDataZwrotu = DateTime.Now.AddDays(14), // Standardowe 14 dni
                    Przedluzono = false
                };
                _context.Wypozyczenia.Add(noweWypozyczenie);

                // 2. Aktualizacja stanu Książki
                rezerwacja.Ksiazka!.stan = "Wypożyczona";
                _context.Ksiazki.Update(rezerwacja.Ksiazka);

                // 3. Dezaktywacja rezerwacji (została zrealizowana)
                rezerwacja.IsActive = false;
                _context.Rezerwacje.Update(rezerwacja);

                // 4. Aktualizacja licznika u Użytkownika
                rezerwacja.User!.iloscWypKsiazek++;
                _context.Users.Update(rezerwacja.User);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Message"] = $"Pomyślnie wypożyczono książkę '{rezerwacja.Ksiazka.tytul}' użytkownikowi {rezerwacja.User?.email}. Rezerwacja została zrealizowana.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = "Wystąpił błąd podczas finalizacji odbioru.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}