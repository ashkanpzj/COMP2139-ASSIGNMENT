using Assignment1.Authorization;
using Assignment1.Data;
using Assignment1.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Controllers;

[Authorize(Policy = PolicyNames.OrganizerOnly)]
public class AnalyticsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AnalyticsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public IActionResult MyAnalytics()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetSalesByCategory()
    {
        var userId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(RoleNames.Admin);

        var query = _context.TicketPurchases
            .Include(p => p.Event)
            .AsNoTracking();

        if (!isAdmin)
        {
            query = query.Where(p => p.Event!.CreatedByUserId == userId);
        }

        var data = await query
            .GroupBy(p => p.Event!.Category ?? "Uncategorized")
            .Select(g => new
            {
                Category = g.Key,
                TicketsSold = g.Sum(p => p.Quantity)
            })
            .OrderByDescending(x => x.TicketsSold)
            .ToListAsync();

        return Json(data);
    }

    [HttpGet]
    public async Task<IActionResult> GetRevenueByMonth()
    {
        var userId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(RoleNames.Admin);

        var query = _context.TicketPurchases
            .Include(p => p.Event)
            .AsNoTracking();

        if (!isAdmin)
        {
            query = query.Where(p => p.Event!.CreatedByUserId == userId);
        }

        // Get last 12 months of data
        var startDate = DateTime.UtcNow.AddMonths(-11);
        var data = await query
            .Where(p => p.PurchasedAtUtc >= startDate)
            .GroupBy(p => new { p.PurchasedAtUtc.Year, p.PurchasedAtUtc.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(p => p.TotalPrice)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        // Format for chart
        var result = data.Select(d => new
        {
            Label = $"{d.Year}-{d.Month:D2}",
            Revenue = d.Revenue
        });

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetTopSellingEvents()
    {
        var userId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(RoleNames.Admin);

        var query = _context.TicketPurchases
            .Include(p => p.Event)
            .AsNoTracking();

        if (!isAdmin)
        {
            query = query.Where(p => p.Event!.CreatedByUserId == userId);
        }

        var data = await query
            .GroupBy(p => new { p.EventId, p.Event!.Title })
            .Select(g => new
            {
                EventId = g.Key.EventId,
                EventTitle = g.Key.Title,
                TicketsSold = g.Sum(p => p.Quantity),
                TotalRevenue = g.Sum(p => p.TotalPrice)
            })
            .OrderByDescending(x => x.TicketsSold)
            .Take(5)
            .ToListAsync();

        return Json(data);
    }
}



