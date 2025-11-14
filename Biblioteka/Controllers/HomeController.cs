using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // <--- DODANA BRAKUJĄCA LINIA
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Biblioteka.Controllers;

public class HomeController : Controller
{
    private readonly BibliotekaContext _context;

    public HomeController(BibliotekaContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new User());
    }

    [HttpPost]
    public IActionResult Register(User user)
    {
        if (ModelState.IsValid)
        {
            // Ustaw domyślne wartości
            user.dataRejestracji = DateTime.Now;
            user.Rola = "user";
            user.iloscWypKsiazek = 0;

            _context.Users.Add(user);  // dodajemy do DbSet
            _context.SaveChanges();    // zapis do SQLite

            TempData["Message"] = $"Zarejestrowano użytkownika: {user.Imie} {user.Nazwisko} email: {user.email}";
            return RedirectToAction("Index");
        }

        return View("Index", user);
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View(new User());
    }

    [HttpPost]
    public async Task<IActionResult> Login(User user)
    {
        var userFromDb = _context.Users
            .FirstOrDefault(u => u.email == user.email && u.haslo == user.haslo);

        if (userFromDb == null)
        {
            ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło.");
            return View(user);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userFromDb.email!),
            new Claim(ClaimTypes.NameIdentifier, userFromDb.Id.ToString()),
            new Claim(ClaimTypes.Role, userFromDb.Rola!)
        };

        var claimsIdentity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

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
            return RedirectToAction("Login", "Home");
        }

        var user = await _context.Users.FindAsync(userId);

        // DODANE: Pobranie aktywnych wypożyczeń dla użytkownika
        ViewBag.AktywneWypozyczenia = await _context.Wypozyczenia
            .Where(w => w.UserId == userId && w.FaktycznaDataZwrotu == null)
            .Include(w => w.Ksiazka)
            .ToListAsync();

        ViewBag.OkresyPrzedluzenia = new SelectList(new List<int> { 7, 14, 30 }); // Okresy przedłużenia

        if (user == null)
        {
            return RedirectToAction("Logout");
        }

        return View(user);
    }
}