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
using System;

namespace Biblioteka.Controllers
{
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
            List<Kategoria> kategorieWWidoku = new List<Kategoria>();

            // 1. Logika ładowania hierarchii kategorii dla filtra
            if (!kategoriaId.HasValue || kategoriaId.Value <= 0)
            {
                // Reset: Pokazujemy wszystkie główne kategorie (korzenie)
                kategorieWWidoku = await _context.Kategorie
                    .Where(k => k.ParentId == null)
                    .OrderBy(k => k.Nazwa)
                    .ToListAsync();
            }
            else
            {
                var wybranaKategoria = await _context.Kategorie.FindAsync(kategoriaId.Value);

                if (wybranaKategoria != null)
                {
                    var parentId = wybranaKategoria.ParentId;

                    // a) Jeśli kategoria ma rodzica, pokaż rodzeństwo i rodzica (powrót)
                    if (parentId.HasValue)
                    {
                        var parent = await _context.Kategorie.FindAsync(parentId.Value);
                        if (parent != null)
                        {
                            parent.Nazwa = $"^ Powrót do: {parent.Nazwa}";
                            kategorieWWidoku.Add(parent);
                        }

                        var rodzenstwo = await _context.Kategorie
                            .Where(k => k.ParentId == parentId.Value)
                            .OrderBy(k => k.Nazwa)
                            .ToListAsync();
                        kategorieWWidoku.AddRange(rodzenstwo);
                    }
                    else
                    {
                        // b) To jest kategoria główna. Pokaż wszystkie jej dzieci.
                        kategorieWWidoku = await _context.Kategorie
                            .Where(k => k.ParentId == kategoriaId.Value)
                            .OrderBy(k => k.Nazwa)
                            .ToListAsync();

                        // Opcja powrotu do "Wszystkie Kategorie" (ID 0)
                        kategorieWWidoku.Insert(0, new Kategoria { Id = 0, Nazwa = $"^ Powrót do: Wszystkie Kategorie", ParentId = null });
                    }
                }
            }

            // Przekazanie listy kategorii do widoku
            ViewBag.KategoriaId = new SelectList(kategorieWWidoku, "Id", "Nazwa", kategoriaId);
            ViewBag.SelectedKategoriaId = kategoriaId;
            ViewBag.CurrentFilter = searchString;

            // 2. LOGIKA FILTROWANIA KSIĄŻEK
            var ksiazkiQuery = _context.Ksiazki
                .Include(k => k.Kategoria)
                .AsQueryable();

            // Filtrowanie po kategorii
            if (kategoriaId.HasValue && kategoriaId.Value > 0)
            {
                ksiazkiQuery = ksiazkiQuery.Where(k => k.KategoriaId == kategoriaId.Value);
            }

            // Wyszukiwanie tekstowe (AND, OR, NOT)
            if (!string.IsNullOrEmpty(searchString))
            {
                var frazy = searchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                // Przejście na Client Evaluation, ponieważ nie można tłumaczyć logiki OR/NOT na SQL w prosty sposób
                var ksiazkiWynikowe = ksiazkiQuery.ToList().AsEnumerable();

                foreach (var fraza in frazy)
                {
                    var cleanFraza = fraza.Trim().ToLower();

                    if (cleanFraza.StartsWith("+")) // AND: musi zawierać
                    {
                        var term = cleanFraza.Substring(1);
                        ksiazkiWynikowe = ksiazkiWynikowe.Where(k =>
                            k.tytul.ToLower().Contains(term) || k.autor.ToLower().Contains(term) || k.tag.ToLower().Contains(term));
                    }
                    else if (cleanFraza.StartsWith("-")) // NOT: nie może zawierać
                    {
                        var term = cleanFraza.Substring(1);
                        ksiazkiWynikowe = ksiazkiWynikowe.Where(k =>
                            !(k.tytul.ToLower().Contains(term) || k.autor.ToLower().Contains(term) || k.tag.ToLower().Contains(term)));
                    }
                    else // Domyślnie AND (jeśli brak operatora)
                    {
                        ksiazkiWynikowe = ksiazkiWynikowe.Where(k =>
                            k.tytul.ToLower().Contains(cleanFraza) || k.autor.ToLower().Contains(cleanFraza) || k.tag.ToLower().Contains(cleanFraza));
                    }
                }

                var ksiazki = ksiazkiWynikowe.OrderBy(k => k.tytul).ToList();
                return View(ksiazki);
            }

            // Standardowe pobranie (jeśli brak wyszukiwania)
            var ksiazkiFinal = await ksiazkiQuery.OrderBy(k => k.tytul).ToListAsync();
            return View(ksiazkiFinal);
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

        // AKCJA USUWANIA (DELETE)
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

        // GET: Ksiazka/Edit/5
        [Authorize(Roles = "admin,worker")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ksiazka = await _context.Ksiazki.FindAsync(id);
            if (ksiazka == null)
            {
                return NotFound();
            }

            ViewData["KategoriaId"] = new SelectList(_context.Kategorie.OrderBy(k => k.Nazwa), "Id", "Nazwa", ksiazka.KategoriaId);
            var stany = new List<string> { "Dostępna", "Wypożyczona", "Zarezerwowana", "Wycofana" };
            ViewBag.StanList = new SelectList(stany, ksiazka.stan);

            return View(ksiazka);
        }

        // POST: Ksiazka/Edit/5 - FINALNY I BEZPIECZNY FIX
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,worker")]
        public async Task<IActionResult> Edit(int id) // Usunięto parametr Ksiazka ksiazka
        {
            var ksiazkaToUpdate = await _context.Ksiazki.FindAsync(id);

            if (ksiazkaToUpdate == null)
            {
                return NotFound();
            }

            // 1. Użycie TryUpdateModelAsync: bezpiecznie mapuje dane z formularza 
            // do śledzonego obiektu, używając białej listy pól.
            if (await TryUpdateModelAsync<Ksiazka>(
                ksiazkaToUpdate,
                "", // Prefiks klucza, pusty
                k => k.tytul, k => k.autor, k => k.isbn, k => k.KategoriaId, k => k.tag, k => k.stan, k => k.SpisTresci /* <--- DODANE NOWE POLE */))
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        await _context.SaveChangesAsync();
                        TempData["Message"] = $"Książka '{ksiazkaToUpdate.tytul}' została pomyślnie zaktualizowana.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                // ... (obsługa błędów)
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Ksiazki.Any(e => e.Id == ksiazkaToUpdate.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Jeśli TryUpdateModelAsync lub ModelState.IsValid nie powiodło się, zwracamy widok z błędami
            ViewData["KategoriaId"] = new SelectList(_context.Kategorie.OrderBy(k => k.Nazwa), "Id", "Nazwa", ksiazkaToUpdate.KategoriaId);
            var stany = new List<string> { "Dostępna", "Wypożyczona", "Zarezerwowana", "Wycofana" };
            ViewBag.StanList = new SelectList(stany, ksiazkaToUpdate.stan);

            return View(ksiazkaToUpdate);
        }

        // AKCJA 11: Szczegóły Książki
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ksiazka = await _context.Ksiazki
                .Include(k => k.Kategoria)
                .Select(k => new KsiazkaDetailsViewModel
                {
                    Ksiazka = k,
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

        // AKCJA 12: Zapisywanie wyszukiwania
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSearch(string? searchString, string nazwaZapisu)
        {
            if (string.IsNullOrEmpty(searchString) || string.IsNullOrEmpty(nazwaZapisu))
            {
                TempData["Message"] = "Wprowadź frazę wyszukiwania i nazwę, pod którą chcesz ją zapisać.";
                return RedirectToAction(nameof(Index));
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                TempData["Message"] = "Błąd autoryzacji.";
                return RedirectToAction("Login", "Home");
            }

            var nowyZapis = new HistoriaWyszukiwan
            {
                UserId = userId,
                Nazwa = nazwaZapisu,
                Zapytanie = searchString,
                DataZapisu = DateTime.Now
            };

            _context.HistoriaWyszukiwan.Add(nowyZapis);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Wyszukiwanie '{nazwaZapisu}' zostało zapisane w historii.";
            return RedirectToAction(nameof(Index));
        }

    }
}