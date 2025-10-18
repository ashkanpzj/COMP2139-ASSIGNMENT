using System;
using System.Linq;
using System.Threading.Tasks;
using Assignment1.Data;
using Assignment1.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] EventCategories = new[]
        {
            "All","Fun","Festival","Concert","Business & Professional","Webinar","Community"
        };

        public TicketsController(ApplicationDbContext context)
        {
            _context = context;
        }
        
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Index(
            string? search,
            string? category,
            decimal? minPrice,
            decimal? maxPrice,
            string? startDate,
            string? endDate,
            string? sort
        )
        {
            var q = _context.Events.AsNoTracking().AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(e => e.Title.Contains(search) || (e.Description ?? "").Contains(search));
            
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                q = q.Where(e => e.Category == category);
            
            if (minPrice.HasValue) q = q.Where(e => (e.Price ?? 0) >= minPrice.Value);
            if (maxPrice.HasValue) q = q.Where(e => (e.Price ?? 0) <= maxPrice.Value);
            
            if (DateTime.TryParse(startDate, out var start))
            {
                start = DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime();
                q = q.Where(e => e.Date >= start);
            }

            if (DateTime.TryParse(endDate, out var end))
            {
                end = DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                q = q.Where(e => e.Date <= end);
            }
            
            q = (sort ?? "date").ToLower() switch
            {
                "alpha" => q.OrderBy(e => e.Title),
                "price" => q.OrderBy(e => e.Price ?? 0),
                _       => q.OrderBy(e => e.Date)
            };
            
            ViewBag.Categories = EventCategories;
            ViewBag.Search = search ?? "";
            ViewBag.Category = category ?? "All";
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Sort = sort ?? "date";

            var items = await q.ToListAsync();
            return View(items);
        }
        
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Buy(int id)
        {
            var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound();
            if (ev.AvailableTickets <= 0) return BadRequest("No tickets left for this event.");

            var vm = new BuyTicketVm
            {
                EventId = ev.EventId,
                EventTitle = ev.Title,
                UnitPrice = ev.Price ?? 0,
                Available = ev.AvailableTickets,
                Quantity = 1,
                IsGuest = !(User.Identity?.IsAuthenticated ?? false)
            };

            return View(vm);
        }
        
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(BuyTicketVm model)
        {
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.EventId == model.EventId);
            if (ev == null) return NotFound();

            // Validation
            if (model.Quantity < 1) ModelState.AddModelError(nameof(model.Quantity), "Quantity must be at least 1.");
            if (model.Quantity > ev.AvailableTickets) ModelState.AddModelError(nameof(model.Quantity), "Not enough tickets available.");

            var isGuest = !(User.Identity?.IsAuthenticated ?? false);
            if (isGuest)
            {
                if (string.IsNullOrWhiteSpace(model.GuestFirstName)) ModelState.AddModelError(nameof(model.GuestFirstName), "First name is required.");
                if (string.IsNullOrWhiteSpace(model.GuestLastName)) ModelState.AddModelError(nameof(model.GuestLastName), "Last name is required.");
                if (string.IsNullOrWhiteSpace(model.GuestEmail)) ModelState.AddModelError(nameof(model.GuestEmail), "Email is required.");
            }

            if (!ModelState.IsValid)
            {
                model.EventTitle = ev.Title;
                model.UnitPrice = ev.Price ?? 0;
                model.Available = ev.AvailableTickets;
                model.IsGuest = isGuest;
                return View(model);
            }

            var unitPrice = ev.Price ?? 0m;
            var purchase = new TicketPurchase
            {
                EventId = ev.EventId,
                Quantity = model.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = unitPrice * model.Quantity,
                PurchasedAtUtc = DateTime.UtcNow,
                BuyerUserId = isGuest ? null : User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                GuestFirstName = isGuest ? model.GuestFirstName : null,
                GuestLastName = isGuest ? model.GuestLastName : null,
                GuestEmail = isGuest ? model.GuestEmail : null
            };

            ev.AvailableTickets -= model.Quantity;

            _context.TicketPurchases.Add(purchase);
            _context.Events.Update(ev);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Receipt), new { id = purchase.TicketPurchaseId });
        }
        
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Receipt(int id)
        {
            var p = await _context.TicketPurchases
                .Include(x => x.Event)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TicketPurchaseId == id);

            if (p == null) return NotFound();

            var vm = new ReceiptVm
            {
                PurchaseId = p.TicketPurchaseId,
                EventTitle = p.Event?.Title ?? "(deleted event)",
                EventDate = p.Event?.Date ?? DateTime.MinValue,
                PurchasedAtLocal = p.PurchasedAtUtc.ToLocalTime(),
                Quantity = p.Quantity,
                UnitPrice = p.UnitPrice,
                TotalPrice = p.TotalPrice,
                GuestName = (p.GuestFirstName, p.GuestLastName) switch
                {
                    (null, null) => null,
                    _ => $"{p.GuestFirstName} {p.GuestLastName}".Trim()
                },
                GuestEmail = p.GuestEmail
            };

            return View(vm);
        }
        
        public class BuyTicketVm
        {
            public int EventId { get; set; }
            public string EventTitle { get; set; } = "";
            public decimal UnitPrice { get; set; }
            public int Available { get; set; }
            public int Quantity { get; set; } = 1;

            public bool IsGuest { get; set; }
            public string? GuestFirstName { get; set; }
            public string? GuestLastName { get; set; }
            public string? GuestEmail { get; set; }
        }

        public class ReceiptVm
        {
            public int PurchaseId { get; set; }
            public string EventTitle { get; set; } = "";
            public DateTime EventDate { get; set; }
            public DateTime PurchasedAtLocal { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal TotalPrice { get; set; }
            public string? GuestName { get; set; }
            public string? GuestEmail { get; set; }
        }
    }
}
