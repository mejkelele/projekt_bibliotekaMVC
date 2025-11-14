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
    // Dostęp do zarządzania kategoriami tylko dla Pracownika/Administratora
    [Authorize(Roles = "worker,admin")]
    public class KategoriaController : Controller
    {
        private readonly BibliotekaContext _context;

        public KategoriaController(BibliotekaContext context)
        {
            _context = context;
        }

        // AKCJA 1: Wyświetla listę kategorii wraz z ich rodzicami
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var kategorie = await _context.Kategorie
                .Include(k => k.Parent)
                .OrderBy(k => k.Nazwa)
                .ToListAsync();

            return View(kategorie);
        }

        // GET: Kategoria/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Przekazanie listy dostępnych rodziców do widoku
            ViewBag.ParentId = new SelectList(
                await _context.Kategorie.OrderBy(k => k.Nazwa).ToListAsync(),
                "Id",
                "Nazwa");

            return View();
        }

        // POST: Kategoria/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nazwa,ParentId")] Kategoria kategoria)
        {
            if (ModelState.IsValid)
            {
                _context.Add(kategoria);
                await _context.SaveChangesAsync();
                TempData["Message"] = $"Dodano nową kategorię: {kategoria.Nazwa}";
                return RedirectToAction(nameof(Index));
            }

            // Ponowne ładowanie listy rodziców w przypadku błędu
            ViewBag.ParentId = new SelectList(
                await _context.Kategorie.OrderBy(k => k.Nazwa).ToListAsync(),
                "Id",
                "Nazwa",
                kategoria.ParentId);

            return View(kategoria);
        }

        // GET: Kategoria/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kategoria = await _context.Kategorie.FindAsync(id);
            if (kategoria == null)
            {
                return NotFound();
            }

            // Lista wszystkich kategorii (jako potencjalni rodzice, z wyłączeniem edytowanej)
            ViewBag.ParentId = new SelectList(
                await _context.Kategorie.Where(k => k.Id != id).OrderBy(k => k.Nazwa).ToListAsync(),
                "Id",
                "Nazwa",
                kategoria.ParentId);

            return View(kategoria);
        }

        // POST: Kategoria/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nazwa,ParentId")] Kategoria kategoria)
        {
            if (id != kategoria.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(kategoria);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = $"Kategoria '{kategoria.Nazwa}' została zaktualizowana.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Kategorie.Any(e => e.Id == kategoria.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // Ponowne ładowanie ParentId w razie błędu walidacji
            ViewBag.ParentId = new SelectList(
                await _context.Kategorie.Where(k => k.Id != id).OrderBy(k => k.Nazwa).ToListAsync(),
                "Id",
                "Nazwa",
                kategoria.ParentId);

            return View(kategoria);
        }

        // GET: Kategoria/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kategoria = await _context.Kategorie
                .Include(k => k.Parent)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (kategoria == null)
            {
                return NotFound();
            }

            // Sprawdzenie, czy kategoria jest używana
            var uzywaneKsiazki = await _context.Ksiazki.CountAsync(k => k.KategoriaId == id);
            var uzywaneDzieci = await _context.Kategorie.CountAsync(k => k.ParentId == id);

            ViewBag.CanDelete = uzywaneKsiazki == 0 && uzywaneDzieci == 0;
            ViewBag.KsiazkiCount = uzywaneKsiazki;
            ViewBag.ChildrenCount = uzywaneDzieci;

            return View(kategoria);
        }

        // POST: Kategoria/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var kategoria = await _context.Kategorie.FindAsync(id);

            // Sprawdzenie, czy kategoria jest pusta przed usunięciem
            var uzywaneKsiazki = await _context.Ksiazki.AnyAsync(k => k.KategoriaId == id);
            var uzywaneDzieci = await _context.Kategorie.AnyAsync(k => k.ParentId == id);

            if (uzywaneKsiazki || uzywaneDzieci)
            {
                TempData["Message"] = "Nie można usunąć kategorii, ponieważ ma powiązane książki lub podkategorie.";
                return RedirectToAction(nameof(Index));
            }

            if (kategoria != null)
            {
                _context.Kategorie.Remove(kategoria);
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = $"Kategoria '{kategoria?.Nazwa}' została pomyślnie usunięta.";
            return RedirectToAction(nameof(Index));
        }
    }
}