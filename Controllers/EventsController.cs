using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assignment1.Authorization;
using Assignment1.Data;
using Assignment1.Models;
using Assignment1.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] EventCategories = new[]
        {
            "Fun", "Festival", "Concert", "Business & Professional", "Webinar", "Community"
        };

        private SelectList CategorySelectList(string? selected = null)
            => new SelectList(EventCategories, selected ?? "Concert");

        public EventsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAuthorizationService authorizationService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _authorizationService = authorizationService;
            _environment = environment;
        }

        // INDEX
        [AllowAnonymous]
        public async Task<IActionResult> Index(
            string? search,
            string? category,
            string? startDate,
            string? endDate,
            string? sort
        )
        {
            var items = await GetFilteredEventsAsync(search, category, startDate, endDate, sort);
            
            ViewBag.Categories = EventCategories.Prepend("All").ToArray();
            ViewBag.Search = search ?? "";
            ViewBag.Category = category ?? "All";
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.Sort = sort ?? "date";

            return View(items);
        }

        // AJAX LIVE SEARCH
        [AllowAnonymous, HttpGet]
        public async Task<IActionResult> LiveSearch(
            string? search,
            string? category,
            string? startDate,
            string? endDate,
            string? sort)
        {
            var items = await GetFilteredEventsAsync(search, category, startDate, endDate, sort);
            return PartialView("_EventsPartial", items);
        }

        private async Task<List<EventCardViewModel>> GetFilteredEventsAsync(
            string? search,
            string? category,
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

            // Category
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                q = q.Where(e => e.Category == category);

            // Date range
            if (DateTime.TryParse(startDate, out var start))
                q = q.Where(e => e.Date >= DateTime.SpecifyKind(start, DateTimeKind.Utc));

            if (DateTime.TryParse(endDate, out var end))
                q = q.Where(e => e.Date <= DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc));

            var now = DateTime.UtcNow;

            // Sorting with priority: upcoming first, low-stock upcoming on top, past events last
            switch (sort?.ToLowerInvariant())
            {
                case "alpha":
                    q = q
                        .OrderBy(e => e.Date < now ? 1 : 0) // past last
                        .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1) // low stock first
                        .ThenBy(e => e.Title);
                    break;
                case "price":
                    q = q
                        .OrderBy(e => e.Date < now ? 1 : 0)
                        .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                        .ThenBy(e => e.Price ?? 0);
                    break;
                case "rating":
                    q = q
                        .OrderBy(e => e.Date < now ? 1 : 0)
                        .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                        .ThenByDescending(e => e.Ratings.Any() 
                            ? e.Ratings.Average(r => r.Rating) 
                            : 0)
                        .ThenBy(e => e.Date);
                    break;
                case "date":
                default:
                    q = q
                        .OrderBy(e => e.Date < now ? 1 : 0)
                        .ThenBy(e => e.Date >= now && e.AvailableTickets <= 5 ? 0 : 1)
                        .ThenBy(e => e.Date);
                    break;
            }

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

        // CREATE
        [Authorize]
        public IActionResult Create()
        {
            ViewBag.Categories = CategorySelectList();
            return View(new Event { Date = DateTime.UtcNow.AddHours(1) });
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event model, IFormFile? eventImage)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = CategorySelectList(model.Category);
                return View(model);
            }

            // Handle image upload
            if (eventImage is { Length: > 0 })
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "events");
                Directory.CreateDirectory(uploadsPath);

                var extension = Path.GetExtension(eventImage.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                await using var stream = System.IO.File.Create(filePath);
                await eventImage.CopyToAsync(stream);

                model.ImageUrl = $"/uploads/events/{fileName}";
            }

            model.Date = DateTime.SpecifyKind(model.Date, DateTimeKind.Utc);
            model.CreatedByUserId = _userManager.GetUserId(User);
            _context.Events.Add(model);
            await _context.SaveChangesAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user != null && !await _userManager.IsInRoleAsync(user, RoleNames.Organizer))
            {
                await _userManager.AddToRoleAsync(user, RoleNames.Organizer);
                // Refresh the sign-in so the new role is reflected in the cookie
                await _signInManager.RefreshSignInAsync(user);
            }

            return RedirectToAction(nameof(Index));
        }

        // EDIT
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();
            if (!(await _authorizationService.AuthorizeAsync(User, ev, PolicyNames.EventOwner)).Succeeded)
                return Forbid();
            ViewBag.Categories = CategorySelectList(ev.Category);
            return View(ev);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Event model, IFormFile? eventImage)
        {
            if (id != model.EventId) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = CategorySelectList(model.Category);
                return View(model);
            }

            var existing = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
            if (existing == null) return NotFound();
            if (!(await _authorizationService.AuthorizeAsync(User, existing, PolicyNames.EventOwner)).Succeeded)
                return Forbid();

            // Handle image upload
            if (eventImage is { Length: > 0 })
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "events");
                Directory.CreateDirectory(uploadsPath);

                var extension = Path.GetExtension(eventImage.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                await using var stream = System.IO.File.Create(filePath);
                await eventImage.CopyToAsync(stream);

                model.ImageUrl = $"/uploads/events/{fileName}";
            }
            else
            {
                model.ImageUrl = existing.ImageUrl; // Keep existing image
            }

            model.Date = DateTime.SpecifyKind(model.Date, DateTimeKind.Utc);
            model.CreatedByUserId = existing.CreatedByUserId;
            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // DELETE
        [Authorize, HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
                return isAjax ? Json(new { success = false, message = "Event not found" }) : NotFound();
            
            if (!(await _authorizationService.AuthorizeAsync(User, ev, PolicyNames.EventOwner)).Succeeded)
                return isAjax ? Json(new { success = false, message = "Not authorized" }) : Forbid();

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            
            return isAjax 
                ? Json(new { success = true, message = "Event deleted" }) 
                : RedirectToAction(nameof(Index));
        }

        // GET COMMENTS (AJAX)
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> GetComments(int eventId)
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var comments = await _context.EventComments
                .Include(c => c.User)
                .Where(c => c.EventId == eventId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Select(c => new
                {
                    id = c.EventCommentId,
                    content = c.Content,
                    displayName = c.User != null ? c.User.FullName : (c.GuestName ?? "Anonymous"),
                    createdAt = c.CreatedAtUtc.ToLocalTime().ToString("MMM dd, yyyy h:mm tt"),
                    isOwner = isAdmin || (currentUserId != null && c.UserId == currentUserId)
                })
                .ToListAsync();

            return Json(new { success = true, comments });
        }

        // POST COMMENT (AJAX)
        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> AddComment(int eventId, string content, string? guestName)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Comment cannot be empty" });

            if (content.Length > 1000)
                return Json(new { success = false, message = "Comment is too long (max 1000 characters)" });

            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
                return Json(new { success = false, message = "Event not found" });

            var isLoggedIn = User.Identity?.IsAuthenticated ?? false;
            var isAdmin = User.IsInRole("Admin");
            var userId = isLoggedIn ? _userManager.GetUserId(User) : null;

            // Get guest name from session if not provided
            if (!isLoggedIn && string.IsNullOrWhiteSpace(guestName))
            {
                guestName = HttpContext.Session.GetString("GuestName") ?? "Guest";
            }

            var comment = new EventComment
            {
                EventId = eventId,
                UserId = userId,
                GuestName = isLoggedIn ? null : guestName,
                Content = content.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.EventComments.Add(comment);
            await _context.SaveChangesAsync();

            // Get display name
            string displayName;
            if (isLoggedIn)
            {
                var user = await _userManager.GetUserAsync(User);
                displayName = user?.FullName ?? User.Identity?.Name ?? "User";
            }
            else
            {
                displayName = guestName ?? "Guest";
            }

            return Json(new
            {
                success = true,
                comment = new
                {
                    id = comment.EventCommentId,
                    content = comment.Content,
                    displayName,
                    createdAt = comment.CreatedAtUtc.ToLocalTime().ToString("MMM dd, yyyy h:mm tt"),
                    isOwner = isAdmin || isLoggedIn
                }
            });
        }

        // DELETE COMMENT (AJAX)
        [HttpPost, Authorize]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var comment = await _context.EventComments.FindAsync(commentId);
            if (comment == null)
                return Json(new { success = false, message = "Comment not found" });

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Only owner or admin can delete
            if (comment.UserId != userId && !isAdmin)
                return Json(new { success = false, message = "Not authorized to delete this comment" });

            _context.EventComments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
