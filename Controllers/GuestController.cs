using Assignment1.Data;
using Assignment1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Assignment1.Controllers;

public class GuestController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GuestController> _logger;

    public GuestController(ApplicationDbContext context, ILogger<GuestController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Guest Login - show login form
    [HttpGet]
    public IActionResult Login()
    {
        // Check if already logged in as guest
        var existingEmail = HttpContext.Session.GetString("GuestEmail");
        if (!string.IsNullOrEmpty(existingEmail))
        {
            return RedirectToAction("MyPurchases");
        }
        return View(new GuestLoginVm());
    }

    // Guest Login - process login
    [HttpPost]
    public async Task<IActionResult> Login(GuestLoginVm model)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email is required");
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        
        // Check if this email has any purchases
        var hasPurchases = await _context.TicketPurchases
            .AnyAsync(p => p.GuestEmail != null && p.GuestEmail.ToLower() == email);

        // Get guest name from most recent purchase if exists
        string guestName = "Guest";
        if (hasPurchases)
        {
            var lastPurchase = await _context.TicketPurchases
                .Where(p => p.GuestEmail != null && p.GuestEmail.ToLower() == email)
                .OrderByDescending(p => p.PurchasedAtUtc)
                .FirstOrDefaultAsync();
            
            if (lastPurchase != null)
            {
                guestName = $"{lastPurchase.GuestFirstName} {lastPurchase.GuestLastName}".Trim();
                if (string.IsNullOrWhiteSpace(guestName)) guestName = "Guest";
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.Name))
        {
            guestName = model.Name.Trim();
        }

        // Store guest session
        HttpContext.Session.SetString("GuestEmail", email);
        HttpContext.Session.SetString("GuestName", guestName);
        
        _logger.LogInformation("Guest logged in: {Email}", email);

        return RedirectToAction("MyPurchases");
    }

    // Guest Logout
    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("GuestEmail");
        HttpContext.Session.Remove("GuestName");
        _logger.LogInformation("Guest logged out");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult MyPurchases()
    {
        var guestEmail = HttpContext.Session.GetString("GuestEmail");
        var guestName = HttpContext.Session.GetString("GuestName");
        
        if (string.IsNullOrEmpty(guestEmail))
        {
            // Not logged in as guest, show lookup form
            return View(new GuestPurchaseLookupVm());
        }

        // Auto-populate with session data
        return View(new GuestPurchaseLookupVm 
        { 
            Email = guestEmail,
            GuestName = guestName,
            AutoLoad = true
        });
    }

    [HttpPost]
    public async Task<IActionResult> MyPurchases(GuestPurchaseLookupVm model)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            if (isAjax)
                return Json(new { success = false, message = "Email is required" });
            ModelState.AddModelError(nameof(model.Email), "Email is required");
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        
        var purchases = await _context.TicketPurchases
            .Include(p => p.Event)
            .Where(p => p.GuestEmail != null && p.GuestEmail.ToLower() == email)
            .OrderByDescending(p => p.PurchasedAtUtc)
            .ToListAsync();

        if (!purchases.Any())
        {
            if (isAjax)
                return Json(new { success = false, message = "No purchases found for this email" });
            model.NotFound = true;
            return View(model);
        }

        var nowUtc = DateTime.UtcNow;

        model.UpcomingTickets = purchases
            .Where(p => p.Event != null && p.Event.Date >= nowUtc)
            .Select(p => new GuestTicketCard
            {
                PurchaseId = p.TicketPurchaseId,
                EventTitle = p.Event!.Title,
                EventDate = p.Event.Date,
                Quantity = p.Quantity,
                TotalPrice = p.TotalPrice,
                QrCodeDataUrl = GenerateQrCode(p.TicketPurchaseId)
            })
            .ToList();

        // Get guest's event ratings
        var pastEventIds = purchases
            .Where(p => p.Event != null && p.Event.Date < nowUtc)
            .Select(p => p.EventId)
            .Distinct()
            .ToList();

        var guestRatings = await _context.EventRatings
            .Where(r => r.GuestEmail != null && r.GuestEmail.ToLower() == email && pastEventIds.Contains(r.EventId))
            .ToDictionaryAsync(r => r.EventId, r => (int?)r.Rating);

        // Group by event to show one entry per event
        model.PastPurchases = purchases
            .Where(p => p.Event != null && p.Event.Date < nowUtc)
            .GroupBy(p => p.EventId)
            .Select(g => new GuestPurchaseCard
            {
                EventId = g.Key,
                EventTitle = g.First().Event!.Title,
                EventDate = g.First().Event!.Date,
                Quantity = g.Sum(p => p.Quantity),
                TotalPrice = g.Sum(p => p.TotalPrice),
                Rating = guestRatings.TryGetValue(g.Key, out var rating) ? rating : null
            })
            .OrderByDescending(p => p.EventDate)
            .ToList();

        model.GuestName = purchases.First().GuestFirstName + " " + purchases.First().GuestLastName;
        model.Found = true;

        if (isAjax)
        {
            return Json(new { 
                success = true, 
                guestName = model.GuestName,
                upcomingTickets = model.UpcomingTickets,
                pastPurchases = model.PastPurchases
            });
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitRating(int eventId, int rating, string email)
    {
        if (rating < 1 || rating > 5)
            return Json(new { success = false, message = "Invalid rating" });

        var normalizedEmail = email.Trim().ToLower();

        // Check if this guest has purchased tickets for this event
        var hasPurchased = await _context.TicketPurchases
            .AnyAsync(p => p.EventId == eventId && 
                          p.GuestEmail != null && 
                          p.GuestEmail.ToLower() == normalizedEmail);

        if (!hasPurchased)
            return Json(new { success = false, message = "You can only rate events you've attended" });

        // Check if guest already rated this event - prevent changes
        var existingRating = await _context.EventRatings
            .AnyAsync(r => r.EventId == eventId && 
                          r.GuestEmail != null && 
                          r.GuestEmail.ToLower() == normalizedEmail);

        if (existingRating)
            return Json(new { success = false, message = "You have already rated this event" });

        // Create new rating
        _context.EventRatings.Add(new EventRating
        {
            EventId = eventId,
            GuestEmail = normalizedEmail,
            Rating = rating,
            RatedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Guest {Email} rated event {EventId} with {Rating} stars",
            email, eventId, rating);

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadTicketPdf(int id, string email)
    {
        var purchase = await _context.TicketPurchases
            .Include(p => p.Event)
            .FirstOrDefaultAsync(p => p.TicketPurchaseId == id && 
                                      p.GuestEmail != null && 
                                      p.GuestEmail.ToLower() == email.ToLower());

        if (purchase?.Event == null)
            return NotFound();

        var pdfBytes = BuildTicketPdf(purchase);
        return File(pdfBytes, "application/pdf", $"ticket-{purchase.TicketPurchaseId}.pdf");
    }

    private static string GenerateQrCode(int purchaseId)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode($"TICKET-{purchaseId}", QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(5);
        return $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
    }

    private static byte[] BuildTicketPdf(TicketPurchase purchase)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode($"TICKET-{purchase.TicketPurchaseId}", QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10);

        using var document = new PdfSharpCore.Pdf.PdfDocument();
        var page = document.AddPage();
        page.Width = PdfSharpCore.Drawing.XUnit.FromMillimeter(210);
        page.Height = PdfSharpCore.Drawing.XUnit.FromMillimeter(100);

        var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;

        // Colors
        var primaryColor = PdfSharpCore.Drawing.XColor.FromArgb(99, 102, 241);
        var darkColor = PdfSharpCore.Drawing.XColor.FromArgb(30, 41, 59);
        var grayColor = PdfSharpCore.Drawing.XColor.FromArgb(100, 116, 139);
        var lightGray = PdfSharpCore.Drawing.XColor.FromArgb(241, 245, 249);

        // Background
        gfx.DrawRectangle(new PdfSharpCore.Drawing.XSolidBrush(PdfSharpCore.Drawing.XColors.White), 0, 0, pageWidth, pageHeight);

        // Left accent bar
        gfx.DrawRectangle(new PdfSharpCore.Drawing.XSolidBrush(primaryColor), 0, 0, 8, pageHeight);

        // Header section
        gfx.DrawRectangle(new PdfSharpCore.Drawing.XSolidBrush(PdfSharpCore.Drawing.XColor.FromArgb(238, 242, 255)), 8, 0, pageWidth - 8, 45);

        // Fonts
        var titleFont = new PdfSharpCore.Drawing.XFont("Arial", 18, PdfSharpCore.Drawing.XFontStyle.Bold);
        var eventFont = new PdfSharpCore.Drawing.XFont("Arial", 14, PdfSharpCore.Drawing.XFontStyle.Bold);
        var labelFont = new PdfSharpCore.Drawing.XFont("Arial", 8, PdfSharpCore.Drawing.XFontStyle.Regular);
        var valueFont = new PdfSharpCore.Drawing.XFont("Arial", 10, PdfSharpCore.Drawing.XFontStyle.Bold);
        var smallFont = new PdfSharpCore.Drawing.XFont("Arial", 7, PdfSharpCore.Drawing.XFontStyle.Regular);

        // Header text
        gfx.DrawString("EVENT TICKET", titleFont, new PdfSharpCore.Drawing.XSolidBrush(primaryColor),
            new PdfSharpCore.Drawing.XPoint(20, 30));

        // Confirmation number
        gfx.DrawString($"#{purchase.TicketPurchaseId:D6}", new PdfSharpCore.Drawing.XFont("Arial", 12, PdfSharpCore.Drawing.XFontStyle.Bold),
            new PdfSharpCore.Drawing.XSolidBrush(darkColor), new PdfSharpCore.Drawing.XPoint(pageWidth - 140, 30));

        // Dashed line separator
        var pen = new PdfSharpCore.Drawing.XPen(PdfSharpCore.Drawing.XColor.FromArgb(203, 213, 225), 1);
        pen.DashStyle = PdfSharpCore.Drawing.XDashStyle.Dash;
        gfx.DrawLine(pen, 20, 55, pageWidth - 140, 55);

        // Event title
        var eventTitle = purchase.Event?.Title ?? "(Event)";
        if (eventTitle.Length > 35) eventTitle = eventTitle.Substring(0, 32) + "...";
        gfx.DrawString(eventTitle, eventFont, new PdfSharpCore.Drawing.XSolidBrush(darkColor),
            new PdfSharpCore.Drawing.XPoint(20, 75));

        // Details grid
        double col1X = 20;
        double col2X = 120;
        double row1Y = 95;
        double row2Y = 125;

        // Date
        gfx.DrawString("DATE", labelFont, new PdfSharpCore.Drawing.XSolidBrush(grayColor), new PdfSharpCore.Drawing.XPoint(col1X, row1Y));
        gfx.DrawString(purchase.Event?.Date.ToLocalTime().ToString("MMM dd, yyyy") ?? "-", valueFont,
            new PdfSharpCore.Drawing.XSolidBrush(darkColor), new PdfSharpCore.Drawing.XPoint(col1X, row1Y + 12));

        // Time
        gfx.DrawString("TIME", labelFont, new PdfSharpCore.Drawing.XSolidBrush(grayColor), new PdfSharpCore.Drawing.XPoint(col2X, row1Y));
        gfx.DrawString(purchase.Event?.Date.ToLocalTime().ToString("h:mm tt") ?? "-", valueFont,
            new PdfSharpCore.Drawing.XSolidBrush(darkColor), new PdfSharpCore.Drawing.XPoint(col2X, row1Y + 12));

        // Tickets
        gfx.DrawString("TICKETS", labelFont, new PdfSharpCore.Drawing.XSolidBrush(grayColor), new PdfSharpCore.Drawing.XPoint(col1X, row2Y));
        gfx.DrawString(purchase.Quantity.ToString(), valueFont,
            new PdfSharpCore.Drawing.XSolidBrush(darkColor), new PdfSharpCore.Drawing.XPoint(col1X, row2Y + 12));

        // Guest name
        gfx.DrawString("GUEST", labelFont, new PdfSharpCore.Drawing.XSolidBrush(grayColor), new PdfSharpCore.Drawing.XPoint(col2X, row2Y));
        gfx.DrawString($"{purchase.GuestFirstName} {purchase.GuestLastName}", valueFont,
            new PdfSharpCore.Drawing.XSolidBrush(darkColor), new PdfSharpCore.Drawing.XPoint(col2X, row2Y + 12));

        // QR Code section
        double qrX = pageWidth - 120;
        double qrY = 55;
        double qrSize = 75;

        // QR background
        gfx.DrawRectangle(new PdfSharpCore.Drawing.XSolidBrush(lightGray), qrX - 10, qrY - 5, qrSize + 20, qrSize + 25);
        gfx.DrawRectangle(PdfSharpCore.Drawing.XPens.LightGray, qrX - 10, qrY - 5, qrSize + 20, qrSize + 25);

        // QR Code
        var qrImage = PdfSharpCore.Drawing.XImage.FromStream(() => new System.IO.MemoryStream(qrBytes));
        gfx.DrawImage(qrImage, qrX, qrY, qrSize, qrSize);

        // Scan text
        gfx.DrawString("SCAN TO ENTER", smallFont, new PdfSharpCore.Drawing.XSolidBrush(grayColor),
            new PdfSharpCore.Drawing.XRect(qrX - 10, qrY + qrSize + 5, qrSize + 20, 12), PdfSharpCore.Drawing.XStringFormats.TopCenter);

        // Bottom bar
        gfx.DrawRectangle(new PdfSharpCore.Drawing.XSolidBrush(lightGray), 0, pageHeight - 20, pageWidth, 20);
        gfx.DrawString("Present this ticket at the venue entrance • Valid for one-time entry",
            smallFont, new PdfSharpCore.Drawing.XSolidBrush(grayColor),
            new PdfSharpCore.Drawing.XRect(0, pageHeight - 15, pageWidth, 12), PdfSharpCore.Drawing.XStringFormats.TopCenter);

        using var stream = new System.IO.MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    public class GuestLoginVm
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
    }

    public class GuestPurchaseLookupVm
    {
        public string? Email { get; set; }
        public string? GuestName { get; set; }
        public bool Found { get; set; }
        public bool NotFound { get; set; }
        public bool AutoLoad { get; set; }
        public List<GuestTicketCard> UpcomingTickets { get; set; } = new();
        public List<GuestPurchaseCard> PastPurchases { get; set; } = new();
    }

    public class GuestTicketCard
    {
        public int PurchaseId { get; set; }
        public string EventTitle { get; set; } = "";
        public DateTime EventDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string QrCodeDataUrl { get; set; } = "";
    }

    public class GuestPurchaseCard
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = "";
        public DateTime EventDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public int? Rating { get; set; }
    }
}

