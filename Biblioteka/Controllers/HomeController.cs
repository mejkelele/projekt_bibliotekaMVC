using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace Biblioteka.Controllers;

public class HomeController : Controller
{
    private readonly BibliotekaContext _context;

    public HomeController(BibliotekaContext context)
    {
        _context = context;
    }

    // ZMIENIONA: Akcja Index z poprawką na losowe sortowanie
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // 1. Pobranie dostępnych książek do pamięci (ToList/AsEnumerable)
        var dostepneKsiazkiQuery = _context.Ksiazki
            .Where(k => k.stan == "Dostępna")
            .Include(k => k.Kategoria);

        // WAŻNA POPRAWKA: Przeniesienie losowego sortowania do pamięci
        var dostepneKsiazki = await dostepneKsiazkiQuery
            .ToListAsync(); // <--- ZATRZYMUJE TŁUMACZENIE SQL, POBIERA DANE

        var losoweKsiazki = dostepneKsiazki
            .OrderBy(k => Guid.NewGuid()) // <--- TERAZ WYKONYWANE W PAMIĘCI (C#)
            .Take(5)
            .ToList();

        // 2. Utworzenie ViewModelu
        var viewModel = new HomeIndexViewModel
        {

            DostepneKsiazki = losoweKsiazki
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Register()
    {
        // Jeśli użytkownik jest już zalogowany, przekieruj na stronę główną
        if (User.Identity!.IsAuthenticated)
        {
            return RedirectToAction(nameof(Index));
        }
        return View(new User());
    }


    // ZMIENIONE: Akcja POST Register


    // ZMIENIONA: Akcja Register z poprawką na losowe sortowanie (w przypadku błędu walidacji)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(User user)
    {
        if (ModelState.IsValid)
        {
            // 🚨 KRYTYCZNA POPRAWKA: HASZOWANIE HASŁA PRZED ZAPISEM DO BAZY
            // Używamy BCrypt.HashPassword, aby przekształcić plain text w bezpieczny hash.
            user.haslo = BCrypt.Net.BCrypt.HashPassword(user.haslo);

            user.dataRejestracji = DateTime.Now;
            user.Rola = "user";
            user.iloscWypKsiazek = 0;

            _context.Users.Add(user);
            await _context.SaveChangesAsync(); // Użyj async SaveChanges

            TempData["Message"] = $"Zarejestrowano użytkownika: {user.Imie} {user.Nazwisko} email: {user.email}";
            return RedirectToAction(nameof(Login));
        }
        return View("Register", user);
    }


    [HttpGet]
    public IActionResult Login()
    {
        return View(new User());
    }

    [HttpPost]
    public async Task<IActionResult> Login(User user)
    {
        // 1. Pobieramy użytkownika po emailu (bez hasła)
        var userFromDb = await _context.Users
            .FirstOrDefaultAsync(u => u.email == user.email);

        if (userFromDb == null)
        {
            ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło.");
            return View("Login", user);
        }

        // 2. WERYFIKACJA HASŁA
        // Używamy standardowej, dwuargumentowej metody Verify.
        // Jeśli ten kod powoduje błąd, spróbuj użyć userFromDb.haslo (bez !),
        // ale najczęściej problem leży w resolvingu przeciążenia.
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(user.haslo, userFromDb.haslo);

        if (!isPasswordValid)
        {
            ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło.");
            return View("Login", user);
        }

        // 3. Utworzenie oświadczeń (Claims)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userFromDb.email!),
            new Claim(ClaimTypes.NameIdentifier, userFromDb.Id.ToString()),
            new Claim(ClaimTypes.Role, userFromDb.Rola!)
        };

        var claimsIdentity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // 4. Ustanowienie sesji
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        return RedirectToAction("UserPage");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> UserPage()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdString, out int userId))
        {
            TempData["Message"] = "Błąd: Nie można zidentyfikować użytkownika.";
            return RedirectToAction("Login", "Home");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["Message"] = "Błąd: Nie znaleziono użytkownika.";
            return RedirectToAction("Login", "Home");
        }

        // 1. POBIERANIE AKTYWNYCH WYPOŻYCZEŃ
        var aktywneWypozyczenia = await _context.Wypozyczenia
            .Where(w => w.UserId == userId && w.FaktycznaDataZwrotu == null)
            .Include(w => w.Ksiazka)
            .OrderByDescending(w => w.DataWypozyczenia)
            .ToListAsync();
        ViewBag.AktywneWypozyczenia = aktywneWypozyczenia;

        // 2. POBIERANIE AKTYWNYCH REZERWACJI
        var aktywneRezerwacje = await _context.Rezerwacje
            .Where(r => r.UserId == userId && r.IsActive)
            .Include(r => r.Ksiazka)
            .OrderBy(r => r.DataRezerwacji)
            .ToListAsync();
        ViewBag.AktywneRezerwacje = aktywneRezerwacje;

        // 3. POBIERANIE HISTORII WYPOŻYCZEŃ
        var historiaWypozyczen = await _context.Wypozyczenia
            .Where(w => w.UserId == userId && w.FaktycznaDataZwrotu != null)
            .Include(w => w.Ksiazka)
            .OrderByDescending(w => w.FaktycznaDataZwrotu)
            .ToListAsync();
        ViewBag.HistoriaWypozyczen = historiaWypozyczen;

        // 4. Konfiguracja SelectList
        var okresy = new List<int> { 7, 14, 30 };
        ViewBag.OkresyPrzedluzenia = new SelectList(
            okresy.Select(d => new { Value = d.ToString(), Text = $"{d} dni" }).ToList(),
            "Value",
            "Text"
        );

        return View(user);

    }
}