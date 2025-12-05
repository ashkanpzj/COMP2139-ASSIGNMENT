using System.IO;
using Assignment1.Data;
using Assignment1.Models;
using Assignment1.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using QRCoder;

namespace Assignment1.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int ticketsPage = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var vm = await BuildViewModelAsync(user, ticketsPage);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var profileVm = new ProfileViewModel
        {
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            CurrentImageUrl = user.ProfilePictureUrl,
            UserEmail = user.Email
        };

        return View(profileVm);
    }

    [HttpGet]
    public async Task<IActionResult> GetTicketsPage(int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false });

        const int pageSize = 4;
        var nowUtc = DateTime.UtcNow;

        var upcomingPurchases = await _context.TicketPurchases
            .Include(tp => tp.Event)
            .Where(tp => tp.BuyerUserId == user.Id && tp.Event != null && tp.Event.Date >= nowUtc)
            .OrderBy(tp => tp.Event!.Date)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(upcomingPurchases.Count / (double)pageSize);
        page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

        var pagedTickets = upcomingPurchases
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new MyTicketCard(
                p.TicketPurchaseId,
                p.Event!.Title,
                p.Event.Date.ToLocalTime(),
                p.Quantity,
                p.TotalPrice,
                BuildQrDataUrl(BuildQrPayload(p))))
            .ToList();

        return Json(new { 
            success = true,
            tickets = pagedTickets.Select(t => new {
                purchaseId = t.PurchaseId,
                eventTitle = t.EventTitle,
                eventDate = t.EventDateLocal.ToString("f"),
                quantity = t.Quantity,
                totalPrice = t.TotalPrice.ToString("C"),
                qrCodeDataUrl = t.QrCodeDataUrl
            }),
            currentPage = page,
            totalPages = totalPages
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRating(int eventId, int rating)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Not authenticated" });

        if (rating is < 1 or > 5)
            return Json(new { success = false, message = "Rating must be between 1 and 5 stars" });

        // Check if user has purchased tickets for this event (must be an attendee)
        var hasPurchased = await _context.TicketPurchases
            .AnyAsync(tp => tp.EventId == eventId && tp.BuyerUserId == user.Id);

        if (!hasPurchased)
            return Json(new { success = false, message = "You can only rate events you've attended" });

        // Check if user already rated this event - prevent changes
        var existingRating = await _context.EventRatings
            .AnyAsync(r => r.EventId == eventId && r.UserId == user.Id);

        if (existingRating)
            return Json(new { success = false, message = "You have already rated this event" });

        // Create new rating
        _context.EventRatings.Add(new EventRating
        {
            EventId = eventId,
            UserId = user.Id,
            Rating = rating,
            RatedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} rated event {EventId} with {Rating} stars", 
            user.Id, eventId, rating);

        return Json(new { success = true, message = "Rating saved!" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(
        [FromForm(Name = "Profile.FullName")] string? fullName,
        [FromForm(Name = "Profile.PhoneNumber")] string? phoneNumber,
        [FromForm(Name = "Profile.DateOfBirth")] string? dateOfBirthStr,
        [FromForm(Name = "Profile.ProfileImage")] IFormFile? profileImage,
        [FromForm(Name = "Profile.RemoveImage")] bool removeImage = false)
    {
        var user = await _userManager.GetUserAsync(User);
        
        _logger.LogInformation("UpdateProfile called - FullName: {FullName}, Phone: {Phone}, DOB: {DOB}", 
            fullName, phoneNumber, dateOfBirthStr);
        
        if (user == null)
            return Json(new { success = false, message = "Not authenticated" });

        // Parse date of birth and convert to UTC for PostgreSQL
        DateTime? dateOfBirth = null;
        if (!string.IsNullOrWhiteSpace(dateOfBirthStr))
        {
            if (DateTime.TryParse(dateOfBirthStr, out var parsedDate))
            {
                // Convert to UTC for PostgreSQL timestamptz compatibility
                dateOfBirth = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }
            else
            {
                return Json(new { success = false, message = "Invalid date format" });
            }
        }

        // Validate image if provided
        string? newImageUrl = null;
        if (!removeImage && profileImage is { Length: > 0 })
        {
            if (!profileImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Please upload a valid image file" });
            }
            if (profileImage.Length > 2 * 1024 * 1024)
            {
                return Json(new { success = false, message = "Image must be 2 MB or smaller" });
            }
            
            try
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);

                var extension = Path.GetExtension(profileImage.FileName);
                var safeFileName = $"{Guid.NewGuid()}{extension}";
                var fullPath = Path.Combine(uploadsPath, safeFileName);

                await using var stream = System.IO.File.Create(fullPath);
                await profileImage.CopyToAsync(stream);

                newImageUrl = $"/uploads/{safeFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save profile image");
                return Json(new { success = false, message = "Failed to save image" });
            }
        }

        try
        {
            // Prepare values
            var cleanFullName = fullName?.Trim();
            var cleanPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

            _logger.LogInformation("Updating user {UserId} - FullName: '{FullName}', Phone: '{Phone}', DOB: {DOB}",
                user.Id, cleanFullName, cleanPhone, dateOfBirth);

            // Use ExecuteUpdateAsync for direct database update (avoids tracking issues)
            var rowsAffected = await _context.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.FullName, cleanFullName)
                    .SetProperty(u => u.PhoneNumber, cleanPhone)
                    .SetProperty(u => u.DateOfBirth, dateOfBirth)
                    .SetProperty(u => u.ProfilePictureUrl,
                        removeImage ? null : (newImageUrl ?? user.ProfilePictureUrl)));

            if (rowsAffected > 0)
            {
                _logger.LogInformation("User {UserId} updated their profile successfully", user.Id);
                return Json(new { success = true, message = "Profile updated!", imageUrl = newImageUrl });
            }
            else
            {
                return Json(new { success = false, message = "No changes were saved" });
            }
        }
        catch (Exception ex)
        {
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error updating profile for user {UserId}: {Message}", user.Id, innerMessage);
            return Json(new { success = false, message = $"Error: {innerMessage}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadTicketPdf(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var purchase = await _context.TicketPurchases
            .Include(tp => tp.Event)
            .FirstOrDefaultAsync(tp => tp.TicketPurchaseId == id && tp.BuyerUserId == user.Id);

        if (purchase?.Event == null)
            return NotFound();

        var pdfBytes = BuildTicketPdf(purchase);
        var fileName = $"ticket-{purchase.TicketPurchaseId}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<DashboardViewModel> BuildViewModelAsync(ApplicationUser user, int ticketsPage = 1, int ticketsPageSize = 4)
    {
        var nowUtc = DateTime.UtcNow;

        var purchases = await _context.TicketPurchases
            .Include(tp => tp.Event)
            .Where(tp => tp.BuyerUserId == user.Id && tp.Event != null)
            .OrderBy(tp => tp.Event!.Date)
            .ToListAsync();

        var upcoming = purchases
            .Where(p => p.Event!.Date >= nowUtc)
            .Select(p => new MyTicketCard(
                p.TicketPurchaseId,
                p.Event!.Title,
                p.Event!.Date.ToLocalTime(),
                p.Quantity,
                p.TotalPrice,
                BuildQrDataUrl(BuildQrPayload(p))))
            .ToList();

        var pagedUpcoming = upcoming
            .Skip((ticketsPage - 1) * ticketsPageSize)
            .Take(ticketsPageSize)
            .ToList();

        // Get user's ratings for events
        var pastEventIds = purchases
            .Where(p => p.Event!.Date < nowUtc)
            .Select(p => p.EventId)
            .Distinct()
            .ToList();

        var userRatings = await _context.EventRatings
            .Where(r => r.UserId == user.Id && pastEventIds.Contains(r.EventId))
            .ToDictionaryAsync(r => r.EventId, r => (int?)r.Rating);

        // Group by event to show one entry per event (not per purchase)
        var history = purchases
            .Where(p => p.Event!.Date < nowUtc)
            .GroupBy(p => p.EventId)
            .Select(g => new PurchaseHistoryCard(
                g.First().EventId,  // Use EventId instead of PurchaseId for rating
                g.First().Event!.Title,
                g.First().Event!.Date.ToLocalTime(),
                g.Sum(p => p.Quantity),  // Total tickets for this event
                g.Sum(p => p.TotalPrice),  // Total spent on this event
                userRatings.TryGetValue(g.Key, out var rating) ? rating : null))
            .OrderByDescending(h => h.EventDateLocal)
            .ToList();

        var events = await _context.Events
            .Where(e => e.CreatedByUserId == user.Id)
            .Select(e => new
            {
                e.EventId,
                e.Title,
                e.Date,
                TicketPrice = e.Price ?? 0m,
                TicketsSold = e.Purchases.Sum(tp => (int?)tp.Quantity) ?? 0,
                TotalRevenue = e.Purchases.Sum(tp => (decimal?)tp.TotalPrice) ?? 0
            })
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        var profile = new ProfileForm
        {
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            CurrentImageUrl = string.IsNullOrWhiteSpace(user.ProfilePictureUrl) ? null : user.ProfilePictureUrl,
            UserEmail = user.Email
        };

        return new DashboardViewModel
        {
            UpcomingTickets = pagedUpcoming,
            TicketsPage = ticketsPage,
            TicketsTotalPages = (int)Math.Ceiling(Math.Max(1, upcoming.Count) / (double)ticketsPageSize),
            PurchaseHistory = history,
            MyEvents = events.Select(e => new MyEventCard(
                e.EventId,
                e.Title,
                e.Date.ToLocalTime(),
                e.TicketPrice,
                e.TicketsSold,
                e.TotalRevenue)).ToList(),
            Profile = profile
        };
    }

    private void ValidateProfileUpload(ProfileForm form)
    {
        if (form.ProfileImage is null || form.ProfileImage.Length == 0)
            return;

        if (!form.ProfileImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("Profile.ProfileImage", "Please upload a valid image file.");

        if (form.ProfileImage.Length > 2 * 1024 * 1024)
            ModelState.AddModelError("Profile.ProfileImage", "Image must be 2 MB or smaller.");
    }

    private static string BuildQrPayload(TicketPurchase purchase)
        => $"TICKET|{purchase.TicketPurchaseId}|{purchase.EventId}|{purchase.Quantity}|{purchase.TotalPrice:0.00}";

    private static string BuildQrDataUrl(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static byte[] BuildTicketPdf(TicketPurchase purchase)
    {
        var payload = BuildQrPayload(purchase);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var qrBytes = qr.GetGraphic(10);

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(100);

        var gfx = XGraphics.FromPdfPage(page);
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;

        // Colors
        var primaryColor = XColor.FromArgb(99, 102, 241); // Indigo
        var darkColor = XColor.FromArgb(30, 41, 59);
        var grayColor = XColor.FromArgb(100, 116, 139);
        var lightGray = XColor.FromArgb(241, 245, 249);

        // Background
        gfx.DrawRectangle(new XSolidBrush(XColors.White), 0, 0, pageWidth, pageHeight);

        // Left accent bar
        gfx.DrawRectangle(new XSolidBrush(primaryColor), 0, 0, 8, pageHeight);

        // Header section with gradient-like effect
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(238, 242, 255)), 8, 0, pageWidth - 8, 45);

        // Fonts
        var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
        var eventFont = new XFont("Arial", 14, XFontStyle.Bold);
        var labelFont = new XFont("Arial", 8, XFontStyle.Regular);
        var valueFont = new XFont("Arial", 10, XFontStyle.Bold);
        var smallFont = new XFont("Arial", 7, XFontStyle.Regular);

        // Header text
        gfx.DrawString("EVENT TICKET", titleFont, new XSolidBrush(primaryColor),
            new XPoint(20, 30));

        // Confirmation number on right of header
        gfx.DrawString($"#{purchase.TicketPurchaseId:D6}", new XFont("Arial", 12, XFontStyle.Bold),
            new XSolidBrush(darkColor), new XPoint(pageWidth - 140, 30));

        // Dashed line separator
        var pen = new XPen(XColor.FromArgb(203, 213, 225), 1);
        pen.DashStyle = XDashStyle.Dash;
        gfx.DrawLine(pen, 20, 55, pageWidth - 140, 55);

        // Event title
        var eventTitle = purchase.Event?.Title ?? "(Event)";
        if (eventTitle.Length > 35) eventTitle = eventTitle.Substring(0, 32) + "...";
        gfx.DrawString(eventTitle, eventFont, new XSolidBrush(darkColor),
            new XPoint(20, 75));

        // Details grid
        double col1X = 20;
        double col2X = 120;
        double row1Y = 95;
        double row2Y = 125;
        double rowHeight = 30;

        // Date
        gfx.DrawString("DATE", labelFont, new XSolidBrush(grayColor), new XPoint(col1X, row1Y));
        gfx.DrawString(purchase.Event?.Date.ToLocalTime().ToString("MMM dd, yyyy") ?? "-", valueFont,
            new XSolidBrush(darkColor), new XPoint(col1X, row1Y + 12));

        // Time
        gfx.DrawString("TIME", labelFont, new XSolidBrush(grayColor), new XPoint(col2X, row1Y));
        gfx.DrawString(purchase.Event?.Date.ToLocalTime().ToString("h:mm tt") ?? "-", valueFont,
            new XSolidBrush(darkColor), new XPoint(col2X, row1Y + 12));

        // Quantity
        gfx.DrawString("TICKETS", labelFont, new XSolidBrush(grayColor), new XPoint(col1X, row2Y));
        gfx.DrawString(purchase.Quantity.ToString(), valueFont,
            new XSolidBrush(darkColor), new XPoint(col1X, row2Y + 12));

        // Total
        gfx.DrawString("TOTAL PAID", labelFont, new XSolidBrush(grayColor), new XPoint(col2X, row2Y));
        gfx.DrawString($"${purchase.TotalPrice:F2}", valueFont,
            new XSolidBrush(XColor.FromArgb(5, 150, 105)), new XPoint(col2X, row2Y + 12));

        // QR Code section (right side)
        double qrX = pageWidth - 120;
        double qrY = 55;
        double qrSize = 75;

        // QR background box
        gfx.DrawRectangle(new XSolidBrush(lightGray), qrX - 10, qrY - 5, qrSize + 20, qrSize + 25);
        gfx.DrawRectangle(XPens.LightGray, qrX - 10, qrY - 5, qrSize + 20, qrSize + 25);

        // QR Code
        using var qrStream = new MemoryStream(qrBytes);
        var qrImage = XImage.FromStream(() => new MemoryStream(qrBytes));
        gfx.DrawImage(qrImage, qrX, qrY, qrSize, qrSize);

        // Scan text under QR
        gfx.DrawString("SCAN TO ENTER", smallFont, new XSolidBrush(grayColor),
            new XRect(qrX - 10, qrY + qrSize + 5, qrSize + 20, 12), XStringFormats.TopCenter);

        // Bottom bar
        gfx.DrawRectangle(new XSolidBrush(lightGray), 0, pageHeight - 20, pageWidth, 20);
        gfx.DrawString("Present this ticket at the venue entrance • Valid for one-time entry",
            smallFont, new XSolidBrush(grayColor),
            new XRect(0, pageHeight - 15, pageWidth, 12), XStringFormats.TopCenter);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }
}


