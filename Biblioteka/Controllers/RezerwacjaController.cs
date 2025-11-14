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
        public async Task<IActionResult> Index()
        {
            // Pobieramy aktywne rezerwacje, sortując według daty (kto pierwszy, ten lepszy)
            var aktywneRezerwacje = await _context.Rezerwacje
                .Where(r => r.IsActive)
                .Include(r => r.User)
                .Include(r => r.Ksiazka)
                .OrderBy(r => r.KsiazkaId) // Grupowanie po książce
                .ThenBy(r => r.DataRezerwacji) // Kolejność rezerwacji
                .ToListAsync();

            return View(aktywneRezerwacje);
        }

        // AKCJA 2: Anulowanie rezerwacji przez personel (np. po wygaśnięciu)
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

        // AKCJA 3: Aktywacja rezerwacji (po zwrocie książki) - logikę przejął WypozyczenieController.Return.
        // Tutaj moglibyśmy dodać logikę do wysłania powiadomienia, ale na razie to pominiemy.
    }
}