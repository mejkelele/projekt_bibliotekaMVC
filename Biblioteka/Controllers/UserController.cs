using Biblioteka.Data;
using Biblioteka.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Biblioteka.Controllers
{
    // Ograniczony tylko dla Administratora
    [Authorize(Roles = "admin")]
    public class UserController : Controller
    {
        private readonly BibliotekaContext _context;

        public UserController(BibliotekaContext context)
        {
            _context = context;
        }

        // GET: User/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderBy(u => u.Nazwisko)
                .ToListAsync();
            return View(users);
        }

        // GET: User/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var availableRoles = new List<string> { "user", "worker", "admin" };

            var viewModel = new UserRoleViewModel
            {
                User = user,
                AvailableRoles = new SelectList(availableRoles, user.Rola)
            };

            return View(viewModel);
        }

        // POST: User/Edit/5 - Używa TryUpdateModelAsync do bezpiecznej aktualizacji
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserRoleViewModel viewModel)
        {
            if (id != viewModel.User.Id)
            {
                return NotFound();
            }

            var userToUpdate = await _context.Users.FindAsync(id);

            if (userToUpdate == null)
            {
                return NotFound();
            }

            // Używamy TryUpdateModelAsync do bezpiecznej aktualizacji wybranych pól
            if (await TryUpdateModelAsync<User>(
                userToUpdate,
                "User",
                u => u.Rola, u => u.Kara, u => u.IsBlocked)) // DODANE: u.Kara i u.IsBlocked
            {
                try
                {
                    if (ModelState.IsValid)
                    {
                        await _context.SaveChangesAsync();
                        TempData["Message"] = $"Status użytkownika '{userToUpdate.email}' został zaktualizowany. Blokada: {userToUpdate.IsBlocked}, Kara: {userToUpdate.Kara} PLN.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Users.Any(e => e.Id == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Ponowne ładowanie SelectList
            var availableRoles = new List<string> { "user", "worker", "admin" };
            viewModel.AvailableRoles = new SelectList(availableRoles, viewModel.User.Rola);
            return View(viewModel);
        }
    }
}