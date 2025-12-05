using Assignment1.Data;
using Assignment1.Models;
using Assignment1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CartController> _logger;

    public CartController(
        ICartService cartService, 
        ApplicationDbContext context,
        ILogger<CartController> logger)
    {
        _cartService = cartService;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var cart = _cartService.GetCart();
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int eventId, int quantity = 1)
    {
        var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null)
            return Json(new { success = false, message = "Event not found" });

        if (ev.AvailableTickets <= 0)
            return Json(new { success = false, message = "No tickets available" });

        var cart = _cartService.GetCart();
        var existingItem = cart.Items.FirstOrDefault(i => i.EventId == eventId);
        var currentQty = existingItem?.Quantity ?? 0;

        if (currentQty + quantity > ev.AvailableTickets)
        {
            return Json(new { 
                success = false, 
                message = $"Only {ev.AvailableTickets - currentQty} more tickets available" 
            });
        }

        var item = new CartItem
        {
            EventId = ev.EventId,
            EventTitle = ev.Title,
            EventDate = ev.Date,
            UnitPrice = ev.Price ?? 0,
            Quantity = quantity,
            AvailableTickets = ev.AvailableTickets
        };

        _cartService.AddToCart(item);

        return Json(new { 
            success = true, 
            message = "Added to cart!",
            cartCount = _cartService.GetCartItemCount()
        });
    }

    [HttpPost]
    public IActionResult UpdateQuantity(int eventId, int quantity)
    {
        _cartService.UpdateQuantity(eventId, quantity);
        var cart = _cartService.GetCart();
        
        return Json(new { 
            success = true,
            cartCount = cart.TotalItems,
            cartTotal = cart.TotalPrice.ToString("0.00")
        });
    }

    [HttpPost]
    public IActionResult Remove(int eventId)
    {
        _cartService.RemoveFromCart(eventId);
        var cart = _cartService.GetCart();
        
        return Json(new { 
            success = true,
            cartCount = cart.TotalItems,
            cartTotal = cart.TotalPrice.ToString("0.00")
        });
    }

    [HttpGet]
    public IActionResult GetCartCount()
    {
        return Json(new { count = _cartService.GetCartItemCount() });
    }

    [HttpGet]
    public IActionResult GetCartSummary()
    {
        var cart = _cartService.GetCart();
        return Json(new {
            items = cart.Items.Select(i => new {
                eventId = i.EventId,
                title = i.EventTitle,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                totalPrice = i.TotalPrice
            }),
            totalItems = cart.TotalItems,
            totalPrice = cart.TotalPrice
        });
    }

    [HttpGet]
    public IActionResult Checkout()
    {
        var cart = _cartService.GetCart();
        if (!cart.Items.Any())
            return RedirectToAction("Index", "Tickets");

        var isGuest = !(User.Identity?.IsAuthenticated ?? false);
        ViewBag.IsGuest = isGuest;
        
        // Pre-fill guest info from session if available
        if (isGuest)
        {
            ViewBag.GuestEmail = HttpContext.Session.GetString("GuestEmail");
            ViewBag.GuestName = HttpContext.Session.GetString("GuestName");
        }
        
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessCheckout(string? guestFirstName, string? guestLastName, string? guestEmail)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var cart = _cartService.GetCart();
        
        if (!cart.Items.Any())
            return isAjax 
                ? Json(new { success = false, message = "Cart is empty" }) 
                : RedirectToAction("Index", "Tickets");

        var isGuest = !(User.Identity?.IsAuthenticated ?? false);

        // Validate guest info
        var errors = new List<string>();
        if (isGuest)
        {
            if (string.IsNullOrWhiteSpace(guestFirstName)) errors.Add("First name is required");
            if (string.IsNullOrWhiteSpace(guestLastName)) errors.Add("Last name is required");
            if (string.IsNullOrWhiteSpace(guestEmail)) errors.Add("Email is required");
        }

        if (errors.Any())
            return isAjax 
                ? Json(new { success = false, message = string.Join(", ", errors) })
                : View("Checkout", cart);

        var userId = isGuest ? null : User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        foreach (var item in cart.Items)
        {
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.EventId == item.EventId);
            if (ev == null || ev.AvailableTickets < item.Quantity)
            {
                var msg = $"Not enough tickets available for {item.EventTitle}";
                return isAjax 
                    ? Json(new { success = false, message = msg }) 
                    : RedirectToAction("Index");
            }

            var purchase = new TicketPurchase
            {
                EventId = item.EventId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                PurchasedAtUtc = DateTime.UtcNow,
                BuyerUserId = userId,
                GuestFirstName = isGuest ? guestFirstName : null,
                GuestLastName = isGuest ? guestLastName : null,
                GuestEmail = isGuest ? guestEmail : null
            };

            ev.AvailableTickets -= item.Quantity;
            _context.TicketPurchases.Add(purchase);
            _context.Events.Update(ev);

            _logger.LogInformation(
                "Purchase completed: Event '{EventTitle}' (ID: {EventId}), Qty: {Quantity}, Total: {Total:C}, Buyer: {Buyer}",
                item.EventTitle, item.EventId, item.Quantity, item.TotalPrice,
                isGuest ? $"Guest: {guestEmail}" : $"User: {userId}");
        }

        await _context.SaveChangesAsync();
        
        var totalItems = cart.TotalItems;
        var totalPrice = cart.TotalPrice;
        _cartService.ClearCart();

        // Save guest session for future visits
        if (isGuest && !string.IsNullOrWhiteSpace(guestEmail))
        {
            HttpContext.Session.SetString("GuestEmail", guestEmail.Trim().ToLowerInvariant());
            var guestFullName = $"{guestFirstName} {guestLastName}".Trim();
            if (!string.IsNullOrWhiteSpace(guestFullName))
            {
                HttpContext.Session.SetString("GuestName", guestFullName);
            }
        }

        if (isAjax)
        {
            return Json(new { 
                success = true, 
                message = "Purchase completed!",
                totalItems,
                totalPrice = totalPrice.ToString("0.00"),
                redirectUrl = Url.Action("CheckoutSuccess")
            });
        }

        TempData["CheckoutSuccess"] = "true";
        TempData["TotalItems"] = totalItems;
        TempData["TotalPrice"] = totalPrice.ToString("0.00");

        return RedirectToAction("CheckoutSuccess");
    }

    [HttpGet]
    public IActionResult CheckoutSuccess()
    {
        if (TempData["CheckoutSuccess"] == null)
            return RedirectToAction("Index", "Tickets");

        ViewBag.TotalItems = TempData["TotalItems"];
        ViewBag.TotalPrice = TempData["TotalPrice"];
        
        return View();
    }
}

