using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Potrzebne dla ISession
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering; // <--- DODAJ TĘ LINIĘ
using System.Security.Claims; // <--- DODAJ TĘ LINIĘ

namespace Biblioteka.Controllers
{
    [Authorize] // Wszystkie akcje wymagają zalogowania
    public class WypozyczenieController : Controller
    {
        private readonly BibliotekaContext _context;
        private const string KoszykSessionKey = "KsiazkiKoszyk";

        public WypozyczenieController(BibliotekaContext context)
        {
            _context = context;
        }

        // Pomocnicza metoda do pobierania listy ID z sesji
        private List<int> GetBasketItems()
        {
            var sessionData = HttpContext.Session.GetString(KoszykSessionKey);
            // Używamy operatora ?? new List<int>() aby obsłużyć null
            return sessionData == null
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(sessionData) ?? new List<int>();
        }

        // AKCJA 1: Finalizacja Wypożyczenia z Koszyka
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizeWypozyczenie(int okresWypozyczenia) // okresWypozyczenia to dni (7, 14, 30)
        {
            var koszykIds = GetBasketItems();

            if (!koszykIds.Any())
            {
                TempData["Message"] = "Błąd: Koszyk jest pusty.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                TempData["Message"] = "Błąd autoryzacji użytkownika.";
                return RedirectToAction("Login", "Home");
            }

            var user = await _context.Users.FindAsync(userId);

            // 1. Pobranie i weryfikacja dostępności książek
            var ksiazkiDoWypozyczenia = await _context.Ksiazki
                .Where(k => koszykIds.Contains(k.Id))
                .ToListAsync();

            if (ksiazkiDoWypozyczenia.Any(k => k.stan != "Dostępna"))
            {
                TempData["Message"] = "Błąd: Co najmniej jedna książka w koszyku nie jest już dostępna do wypożyczenia.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }

            // 2. Transakcja i zapis zmian
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var dataWypozyczenia = DateTime.Now;
                var dataZwrotu = dataWypozyczenia.AddDays(okresWypozyczenia);
                var liczbaWypozyczonych = 0;

                foreach (var ksiazka in ksiazkiDoWypozyczenia)
                {
                    // a) Tworzenie rekordu Wypożyczenia
                    var noweWypozyczenie = new Wypozyczenie
                    {
                        UserId = userId,
                        KsiazkaId = ksiazka.Id,
                        DataWypozyczenia = dataWypozyczenia,
                        OczekiwanaDataZwrotu = dataZwrotu,
                        FaktycznaDataZwrotu = null
                    };
                    _context.Wypozyczenia.Add(noweWypozyczenie);

                    // b) Aktualizacja stanu Książki
                    ksiazka.stan = "Wypożyczona";
                    _context.Ksiazki.Update(ksiazka);

                    liczbaWypozyczonych++;
                }

                // c) Aktualizacja licznika u Użytkownika
                user!.iloscWypKsiazek += liczbaWypozyczonych; // Używamy ! bo sprawdziliśmy user != null
                _context.Users.Update(user);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // d) Oczyszczenie koszyka
                HttpContext.Session.Remove(KoszykSessionKey);

                TempData["Message"] = $"Pomyślnie wypożyczono {liczbaWypozyczonych} książki. Termin zwrotu: {dataZwrotu.ToShortDateString()}.";
                return RedirectToAction("UserPage", "Home");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = "Wystąpił nieoczekiwany błąd podczas finalizacji wypożyczenia. Spróbuj ponownie.";
                return RedirectToAction("ViewBasket", "Ksiazka");
            }
        }

        // AKCJA 2: Lista wszystkich aktywnych wypożyczeń (dla Pracownika/Admina)
        [HttpGet]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Index()
        {
            var aktywneWypozyczenia = await _context.Wypozyczenia
                .Where(w => w.FaktycznaDataZwrotu == null)
                .Include(w => w.User)
                .Include(w => w.Ksiazka)
                    .ThenInclude(k => k.Kategoria)
                .OrderBy(w => w.OczekiwanaDataZwrotu)
                .ToListAsync();

            return View(aktywneWypozyczenia);
        }

        // AKCJA 3: Obsługa zwrotu książki
        [HttpPost]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Return(int id) // ID Wypożyczenia, nie książki
        {
            var wypozyczenie = await _context.Wypozyczenia
                .Include(w => w.User)
                .Include(w => w.Ksiazka)
                .FirstOrDefaultAsync(w => w.Id == id && w.FaktycznaDataZwrotu == null);

            if (wypozyczenie == null)
            {
                TempData["Message"] = "Błąd: Aktywne wypożyczenie nie zostało znalezione.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // a) Aktualizacja rekordu Wypożyczenia
                wypozyczenie.FaktycznaDataZwrotu = DateTime.Now;
                _context.Wypozyczenia.Update(wypozyczenie);

                // b) Sprawdzenie, czy książka ma aktywną rezerwację
                var maRezerwacje = await _context.Rezerwacje
                    .AnyAsync(r => r.KsiazkaId == wypozyczenie.KsiazkaId && r.IsActive);

                // c) Aktualizacja stanu Książki
                if (maRezerwacje)
                {
                    wypozyczenie.Ksiazka.stan = "Zarezerwowana";
                }
                else
                {
                    wypozyczenie.Ksiazka.stan = "Dostępna";
                }
                _context.Ksiazki.Update(wypozyczenie.Ksiazka);

                // d) Aktualizacja licznika u Użytkownika
                wypozyczenie.User!.iloscWypKsiazek--;
                _context.Users.Update(wypozyczenie.User);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Message"] = $"Zwrócono '{wypozyczenie.Ksiazka.tytul}'. Książka jest teraz w stanie: {wypozyczenie.Ksiazka.stan}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Message"] = "Wystąpił błąd podczas rejestracji zwrotu.";
                return RedirectToAction(nameof(Index));
            }
        }

        // AKCJA 4: Przedłużenie wypożyczenia
        [HttpPost]
        [Authorize(Roles = "user")]
        public async Task<IActionResult> Extend(int wypozyczenieId, int dni)
        {
            var wypozyczenie = await _context.Wypozyczenia
                .Include(w => w.Ksiazka)
                .FirstOrDefaultAsync(w => w.Id == wypozyczenieId && w.FaktycznaDataZwrotu == null);

            if (wypozyczenie == null)
            {
                TempData["Message"] = "Nie znaleziono aktywnego wypożyczenia.";
                return RedirectToAction("UserPage", "Home");
            }

            // Blokada Przedłużenia: Sprawdzenie, czy na książkę istnieje aktywna rezerwacja
            var maRezerwacje = await _context.Rezerwacje
                .AnyAsync(r => r.KsiazkaId == wypozyczenie.KsiazkaId && r.IsActive);

            if (maRezerwacje)
            {
                TempData["Message"] = $"Nie można przedłużyć wypożyczenia książki '{wypozyczenie.Ksiazka.tytul}', ponieważ jest na nią aktywna rezerwacja.";
                return RedirectToAction("UserPage", "Home");
            }

            // Blokada Przedłużenia: Tylko jedno przedłużenie jest dozwolone
            if (wypozyczenie.Przedluzono)
            {
                TempData["Message"] = $"Nie można przedłużyć wypożyczenia książki '{wypozyczenie.Ksiazka.tytul}' po raz kolejny.";
                return RedirectToAction("UserPage", "Home");
            }

            // Aktualizacja daty zwrotu
            wypozyczenie.OczekiwanaDataZwrotu = wypozyczenie.OczekiwanaDataZwrotu.AddDays(dni);
            wypozyczenie.Przedluzono = true;
            _context.Wypozyczenia.Update(wypozyczenie);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Pomyślnie przedłużono wypożyczenie książki '{wypozyczenie.Ksiazka.tytul}' o {dni} dni. Nowy termin zwrotu: {wypozyczenie.OczekiwanaDataZwrotu.ToShortDateString()}.";
            return RedirectToAction("UserPage", "Home");
        }
    }
}