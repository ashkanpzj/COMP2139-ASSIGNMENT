using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Assignment1.Data;
using Assignment1.Models;

namespace Assignment1.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] EventCategories = new[]
        {
            "Fun", "Festival", "Concert", "Business & Professional", "Webinar", "Community"
        };

        private SelectList CategorySelectList(string? selected = null)
            => new SelectList(EventCategories, selected ?? "Concert");

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ INDEX
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? search,
            string? category,
            string? startDate,
            string? endDate,
            string? sort
        )
        {
            var q = _context.Events.AsQueryable();

            // 🔍 Search
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(e => e.Title.Contains(search) || (e.Description ?? "").Contains(search));

            // 🏷️ Category
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                q = q.Where(e => e.Category == category);

            // 📅 Date range
            if (DateTime.TryParse(startDate, out var start))
                q = q.Where(e => e.Date >= DateTime.SpecifyKind(start, DateTimeKind.Utc));

            if (DateTime.TryParse(endDate, out var end))
                q = q.Where(e => e.Date <= DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc));

            // 🔽 Sorting (FIXED)
            switch (sort?.ToLowerInvariant())
            {
                case "alpha":
                    q = q.OrderBy(e => e.Title);
                    break;
                case "price":
                    q = q.OrderBy(e => e.Price ?? 0);
                    break;
                case "date":
                default:
                    q = q.OrderBy(e => e.Date);
                    break;
            }

            // 📦 ViewBag sync
            ViewBag.Categories = EventCategories.Prepend("All").ToArray();
            ViewBag.Search = search ?? "";
            ViewBag.Category = category ?? "All";
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Sort = sort ?? "date";

            var items = await q.AsNoTracking().ToListAsync();
            return View(items);
        }

        // ✅ CREATE
        [Authorize]
        public IActionResult Create()
        {
            ViewBag.Categories = CategorySelectList();
            return View(new Event { Date = DateTime.UtcNow.AddHours(1) });
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = CategorySelectList(model.Category);
                return View(model);
            }

            model.Date = DateTime.SpecifyKind(model.Date, DateTimeKind.Utc);
            _context.Events.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ EDIT
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();
            ViewBag.Categories = CategorySelectList(ev.Category);
            return View(ev);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Event model)
        {
            if (id != model.EventId) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = CategorySelectList(model.Category);
                return View(model);
            }

            model.Date = DateTime.SpecifyKind(model.Date, DateTimeKind.Utc);
            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ DELETE
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
