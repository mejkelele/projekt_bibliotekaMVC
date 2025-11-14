using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;

namespace Biblioteka.Controllers
{
    // Akcje Edit/Details zostały pominięte w tym kroku, ale zostawiamy miejsce na ich rozszerzenie.
    public class KsiazkaController : Controller
    {
        private readonly BibliotekaContext _context;
        private const string KoszykSessionKey = "KsiazkiKoszyk";


        public KsiazkaController(BibliotekaContext context)
        {
            _context = context;
        }

        // Pomocnicza metoda do pobierania listy ID z sesji
        private List<int> GetBasketItems()
        {
            var sessionData = HttpContext.Session.GetString(KoszykSessionKey);
            // Używamy operatora ?? new List<int>() aby obsłużyć null, usuwając CS8603
            return sessionData == null
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(sessionData) ?? new List<int>();
        }

        // Pomocnicza metoda do zapisywania listy ID do sesji
        private void SaveBasketItems(List<int> basketItems)
        {
            HttpContext.Session.SetString(KoszykSessionKey, JsonSerializer.Serialize(basketItems));
        }

        // AKCJA 1: Wyświetla listę wszystkich książek z filtrowaniem i wyszukiwaniem.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Index(int? kategoriaId, string? searchString)
        {
            var kategorie = await _context.Kategorie
                .OrderBy(k => k.Nazwa)
                .ToListAsync();

            ViewBag.KategoriaId = new SelectList(kategorie, "Id", "Nazwa", kategoriaId);
            ViewBag.SelectedKategoriaId = kategoriaId;
            ViewBag.CurrentFilter = searchString;

            var ksiazkiQuery = _context.Ksiazki
                .Include(k => k.Kategoria)
                .AsQueryable();

            if (kategoriaId.HasValue && kategoriaId.Value > 0)
            {
                ksiazkiQuery = ksiazkiQuery.Where(k => k.KategoriaId == kategoriaId.Value);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();

                ksiazkiQuery = ksiazkiQuery.Where(k =>
                    k.tytul.ToLower().Contains(searchString) ||
                    k.autor.ToLower().Contains(searchString) ||
                    k.tag.ToLower().Contains(searchString) ||
                    k.isbn.ToString().Contains(searchString)
                );
            }

            var ksiazki = await ksiazkiQuery.OrderBy(k => k.tytul).ToListAsync();

            return View(ksiazki);
        }

        [HttpGet]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.KategoriaId = new SelectList(await _context.Kategorie.ToListAsync(), "Id", "Nazwa");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Create(Ksiazka ksiazka)
        {
            if (ModelState.IsValid)
            {
                ksiazka.stan = "Dostępna";

                _context.Add(ksiazka);
                await _context.SaveChangesAsync();
                TempData["Message"] = $"Dodano książkę: {ksiazka.tytul}";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.KategoriaId = new SelectList(await _context.Kategorie.ToListAsync(), "Id", "Nazwa", ksiazka.KategoriaId);
            return View(ksiazka);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "worker,admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var ksiazka = await _context.Ksiazki.FindAsync(id);
            if (ksiazka == null)
            {
                TempData["Message"] = "Książka nie została znaleziona.";
                return RedirectToAction(nameof(Index));
            }

            _context.Ksiazki.Remove(ksiazka);
            await _context.SaveChangesAsync();
            TempData["Message"] = $"Usunięto książkę: {ksiazka.tytul}";
            return RedirectToAction(nameof(Index));
        }

        // AKCJE KOSZYKA
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToBasket(int id)
        {
            var ksiazka = await _context.Ksiazki.FindAsync(id);

            if (ksiazka == null || ksiazka.stan != "Dostępna")
            {
                TempData["Message"] = $"Książka '{ksiazka?.tytul ?? "Brak"}' jest niedostępna lub nie istnieje.";
                return RedirectToAction(nameof(Index));
            }

            var koszyk = GetBasketItems();

            if (koszyk.Contains(id))
            {
                TempData["Message"] = "Książka jest już w koszyku.";
                return RedirectToAction(nameof(Index));
            }

            koszyk.Add(id);
            SaveBasketItems(koszyk);

            TempData["Message"] = $"Dodano '{ksiazka.tytul}' do koszyka. Ilość: {koszyk.Count}";
            return RedirectToAction(nameof(Index));
        }


        // AKCJA 6: Wyświetlanie zawartości koszyka
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ViewBasket()
        {
            var koszykIds = GetBasketItems();

            var ksiazkiWKoszyku = await _context.Ksiazki
                .Where(k => koszykIds.Contains(k.Id))
                .Include(k => k.Kategoria)
                .ToListAsync();

            var okresy = new List<int> { 7, 14, 30 };

            // NAPRAWA CS1503: Użycie anonimowego obiektu z polami Value i Text
            ViewBag.OkresyWypozyczenia = new SelectList(
                okresy.Select(d => new { Value = d, Text = $"{d} dni" }).ToList(),
                "Value",
                "Text"
            );

            return View(ksiazkiWKoszyku);
        }

        // AKCJA 7: Usuwanie książki z koszyka
        [HttpPost]
        [Authorize]
        public IActionResult RemoveFromBasket(int id)
        {
            var koszyk = GetBasketItems();
            var ksiazka = _context.Ksiazki.Find(id);

            if (koszyk.Remove(id))
            {
                SaveBasketItems(koszyk);
                TempData["Message"] = $"Usunięto '{ksiazka?.tytul ?? "książkę"}' z koszyka.";
            }
            return RedirectToAction(nameof(ViewBasket));
        }

        // AKCJA 8: Akcja, która kieruje do finalizacji w WypozyczenieController
        [HttpPost]
        [Authorize]
        public IActionResult FinalizeWypozyczenie(int okresWypozyczenia)
        {
            return RedirectToAction("FinalizeWypozyczenie", "Wypozyczenie", new { okresWypozyczenia });
        }
        // AKCJA 9: Rezerwacja książki
        [HttpPost]
        [Authorize(Roles = "user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                TempData["Message"] = "Błąd autoryzacji użytkownika.";
                return RedirectToAction("Index");
            }

            var ksiazka = await _context.Ksiazki.FindAsync(id);

            if (ksiazka == null)
            {
                TempData["Message"] = "Książka nie istnieje.";
                return RedirectToAction(nameof(Index));
            }

            // Warunki rezerwacji
            if (ksiazka.stan != "Wypożyczona")
            {
                TempData["Message"] = $"Książka '{ksiazka.tytul}' nie może być zarezerwowana, ponieważ jest {ksiazka.stan}.";
                return RedirectToAction(nameof(Index));
            }

            // Sprawdzenie, czy użytkownik już nie ma aktywnej rezerwacji na tę książkę
            var aktywnaRezerwacja = await _context.Rezerwacje
                .AnyAsync(r => r.KsiazkaId == id && r.UserId == userId && r.IsActive);

            if (aktywnaRezerwacja)
            {
                TempData["Message"] = "Już masz aktywną rezerwację na tę książkę.";
                return RedirectToAction(nameof(Index));
            }

            // Tworzenie nowego rekordu rezerwacji
            var nowaRezerwacja = new Rezerwacja
            {
                UserId = userId,
                KsiazkaId = id,
                DataRezerwacji = DateTime.Now,
                DataWygasniecia = DateTime.Now.AddDays(3) // Rezerwacja ważna 3 dni
            };

            _context.Rezerwacje.Add(nowaRezerwacja);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Pomyślnie zarezerwowano '{ksiazka.tytul}'. Odbiór: do 3 dni po zwrocie.";
            return RedirectToAction(nameof(Index));
        }

        // AKCJA 10: Szczegóły Książki
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Pobieramy dane za pomocą Select, aby od razu utworzyć ViewModel
            var ksiazka = await _context.Ksiazki
                .Include(k => k.Kategoria)
                .Select(k => new KsiazkaDetailsViewModel
                {
                    Ksiazka = k,
                    // Pobieranie aktywnych rezerwacji dla danej książki
                    AktywneRezerwacje = _context.Rezerwacje
                        .Where(r => r.KsiazkaId == k.Id && r.IsActive)
                        .Include(r => r.User)
                        .OrderBy(r => r.DataRezerwacji)
                        .ToList()
                })
                .FirstOrDefaultAsync(vm => vm.Ksiazka.Id == id);

            if (ksiazka == null)
            {
                return NotFound();
            }

            return View(ksiazka);
        }

    }
}