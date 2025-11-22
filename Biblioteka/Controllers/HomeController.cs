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

namespace Biblioteka.Controllers
{
    public class HomeController : Controller
    {
        private readonly BibliotekaContext _context;

        public HomeController(BibliotekaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var najnowszeDostepneKsiazki = await _context.Ksiazki
                .Where(k => k.stan == "Dostępna")
                .OrderByDescending(k => k.Id)
                .Take(6)
                .Include(k => k.Kategoria)
                .ToListAsync();

            var viewModel = new HomeIndexViewModel
            {
                DostepneKsiazki = najnowszeDostepneKsiazki
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction(nameof(Index));
            }
            return View(new User());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user)
        {
            if (ModelState.IsValid)
            {
                user.haslo = BCrypt.Net.BCrypt.HashPassword(user.haslo);
                user.dataRejestracji = DateTime.Now;
                user.Rola = "user";
                user.iloscWypKsiazek = 0;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"Zarejestrowano pomyślnie. Możesz się teraz zalogować.";
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
            var userFromDb = await _context.Users.FirstOrDefaultAsync(u => u.email == user.email);

            if (userFromDb == null || !BCrypt.Net.BCrypt.Verify(user.haslo, userFromDb.haslo))
            {
                ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło.");
                return View("Login", user);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userFromDb.email!),
                new Claim(ClaimTypes.NameIdentifier, userFromDb.Id.ToString()),
                new Claim(ClaimTypes.Role, userFromDb.Rola!)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
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
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.AktywneWypozyczenia = await _context.Wypozyczenia
                .Where(w => w.UserId == userId && w.FaktycznaDataZwrotu == null)
                .Include(w => w.Ksiazka).OrderByDescending(w => w.DataWypozyczenia).ToListAsync();

            ViewBag.AktywneRezerwacje = await _context.Rezerwacje
                .Where(r => r.UserId == userId && r.IsActive)
                .Include(r => r.Ksiazka).OrderBy(r => r.DataRezerwacji).ToListAsync();

            ViewBag.HistoriaWypozyczen = await _context.Wypozyczenia
                .Where(w => w.UserId == userId && w.FaktycznaDataZwrotu != null)
                .Include(w => w.Ksiazka).OrderByDescending(w => w.FaktycznaDataZwrotu).ToListAsync();
            
            ViewBag.ZapisaneWyszukiwania = await _context.HistoriaWyszukiwan
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.DataZapisu)
                .ToListAsync();

            ViewBag.OkresyPrzedluzenia = new SelectList(
                new List<int> { 7, 14, 30 }.Select(d => new { Value = d, Text = $"{d} dni" }), "Value", "Text");

            return View(user);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSearch(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return Unauthorized();
            }

            var searchToDelete = await _context.HistoriaWyszukiwan.FindAsync(id);

            if (searchToDelete == null)
            {
                TempData["Message"] = "Nie znaleziono wyszukiwania.";
                return RedirectToAction(nameof(UserPage));
            }

            if (searchToDelete.UserId != userId)
            {
                TempData["Message"] = "Nie masz uprawnień do usunięcia tego wyszukiwania.";
                return Forbid();
            }

            _context.HistoriaWyszukiwan.Remove(searchToDelete);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Zapisane wyszukiwanie zostało usunięte.";
            return RedirectToAction(nameof(UserPage));
        }
    }
}
