using System;
using System.Linq;
using System.Threading.Tasks;
using Assignment1.Authorization;
using Assignment1.Data;
using Assignment1.Models;
using Assignment1.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TicketsController> _logger;

        private static readonly string[] EventCategories = new[]
        {
            "All","Fun","Festival","Concert","Business & Professional","Webinar","Community"
        };

        public TicketsController(ApplicationDbContext context, ILogger<TicketsController> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Index(
            string? search,
            string? category,
            decimal? minPrice,
            decimal? maxPrice,
            string? startDate,
            string? endDate,
            string? sort,
            int? eventId
        )
        {
            var items = await GetFilteredEventsAsync(search, category, minPrice, maxPrice, startDate, endDate, sort);
            
            ViewBag.Categories = EventCategories;
            ViewBag.Search = search ?? "";
            ViewBag.Category = category ?? "All";
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Sort = sort ?? "date";
            ViewBag.HighlightEventId = eventId;

            return View(items);
        }

        // AJAX LIVE SEARCH
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> LiveSearch(
            string? search,
            string? category,
            string? startDate,
            string? endDate,
            string? sort)
        {
            var items = await GetFilteredEventsAsync(search, category, null, null, startDate, endDate, sort);
            ViewBag.Sort = sort ?? "date";
            return PartialView("_TicketsPartial", items);
        }

        private async Task<List<EventCardViewModel>> GetFilteredEventsAsync(
            string? search,
            string? category,
            decimal? minPrice,
            decimal? maxPrice,
            string? startDate,
            string? endDate,
            string? sort)
        {
            var q = _context.Events.Include(e => e.Ratings).AsQueryable();
            
            // Search (case-insensitive)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                q = q.Where(e => e.Title.ToLower().Contains(searchLower) || 
                                 (e.Description ?? "").ToLower().Contains(searchLower));
            }
            
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
            
            var now = DateTime.UtcNow;

            q = (sort ?? "date").ToLower() switch
            {
                "alpha" => q
                    .OrderBy(e => e.Date < now ? 1 : 0)
                    .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                    .ThenBy(e => e.Title),
                "price" => q
                    .OrderBy(e => e.Date < now ? 1 : 0)
                    .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                    .ThenBy(e => e.Price ?? 0),
                "rating" => q
                    .OrderBy(e => e.Date < now ? 1 : 0)
                    .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                    .ThenByDescending(e => e.Ratings.Any() 
                        ? e.Ratings.Average(r => r.Rating) 
                        : 0)
                    .ThenBy(e => e.Date),
                _ => q
                    .OrderBy(e => e.Date < now ? 1 : 0)
                    .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                    .ThenBy(e => e.Date)
            };

            var events = await q.AsNoTracking().ToListAsync();
            
            return events.Select(e => new EventCardViewModel
            {
                EventId = e.EventId,
                Title = e.Title,
                Date = e.Date,
                Description = e.Description,
                Category = e.Category,
                Price = e.Price,
                AvailableTickets = e.AvailableTickets,
                ImageUrl = e.ImageUrl,
                CreatedByUserId = e.CreatedByUserId,
                AverageRating = e.Ratings.Any() 
                    ? e.Ratings.Average(r => r.Rating) 
                    : 0,
                TotalRatings = e.Ratings.Count
            }).ToList();
        }
        
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> Buy(int id)
        {
            var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound();
            if (ev.AvailableTickets <= 0) return BadRequest("No tickets left for this event.");

            var isGuest = !(User.Identity?.IsAuthenticated ?? false);

            var vm = new BuyTicketVm
            {
                EventId = ev.EventId,
                EventTitle = ev.Title,
                UnitPrice = ev.Price ?? 0,
                Available = ev.AvailableTickets,
                Quantity = 1,
                IsGuest = isGuest
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
                if (string.IsNullOrWhiteSpace(model.GuestFirstName))
                    ModelState.AddModelError(nameof(model.GuestFirstName), "First name is required for guest purchases.");
                if (string.IsNullOrWhiteSpace(model.GuestLastName))
                    ModelState.AddModelError(nameof(model.GuestLastName), "Last name is required for guest purchases.");
                if (string.IsNullOrWhiteSpace(model.GuestEmail))
                    ModelState.AddModelError(nameof(model.GuestEmail), "Email is required for guest purchases.");
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

            var buyerInfo = isGuest ? $"Guest: {model.GuestEmail}" : $"User: {purchase.BuyerUserId}";
            _logger.LogInformation("Purchase completed: {PurchaseId} for Event '{EventTitle}' (ID: {EventId}), Qty: {Quantity}, Total: {Total:C}, Buyer: {Buyer}",
                purchase.TicketPurchaseId, ev.Title, ev.EventId, purchase.Quantity, purchase.TotalPrice, buyerInfo);

            // Support AJAX purchase
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    purchaseId = purchase.TicketPurchaseId,
                    eventTitle = ev.Title,
                    quantity = purchase.Quantity,
                    totalPrice = purchase.TotalPrice,
                    receiptUrl = Url.Action(nameof(Receipt), new { id = purchase.TicketPurchaseId })
                });
            }

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
