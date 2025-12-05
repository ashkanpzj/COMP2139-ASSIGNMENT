using System.Text.Json;
using Assignment1.Models;

namespace Assignment1.Services;

public interface ICartService
{
    ShoppingCart GetCart();
    void AddToCart(CartItem item);
    void UpdateQuantity(int eventId, int quantity);
    void RemoveFromCart(int eventId);
    void ClearCart();
    int GetCartItemCount();
}

public class CartService : ICartService
{
    private const string CartSessionKey = "ShoppingCart";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ISession Session => _httpContextAccessor.HttpContext?.Session 
        ?? throw new InvalidOperationException("No HTTP context available");

    public ShoppingCart GetCart()
    {
        var cartJson = Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(cartJson))
            return new ShoppingCart();
        
        return JsonSerializer.Deserialize<ShoppingCart>(cartJson) ?? new ShoppingCart();
    }

    private void SaveCart(ShoppingCart cart)
    {
        var cartJson = JsonSerializer.Serialize(cart);
        Session.SetString(CartSessionKey, cartJson);
    }

    public void AddToCart(CartItem item)
    {
        var cart = GetCart();
        var existing = cart.Items.FirstOrDefault(i => i.EventId == item.EventId);
        
        if (existing != null)
        {
            // Update quantity, respecting available tickets
            var newQty = existing.Quantity + item.Quantity;
            existing.Quantity = Math.Min(newQty, item.AvailableTickets);
            existing.AvailableTickets = item.AvailableTickets;
        }
        else
        {
            cart.Items.Add(item);
        }
        
        SaveCart(cart);
    }

    public void UpdateQuantity(int eventId, int quantity)
    {
        var cart = GetCart();
        var item = cart.Items.FirstOrDefault(i => i.EventId == eventId);
        
        if (item != null)
        {
            if (quantity <= 0)
            {
                cart.Items.Remove(item);
            }
            else
            {
                item.Quantity = Math.Min(quantity, item.AvailableTickets);
            }
            SaveCart(cart);
        }
    }

    public void RemoveFromCart(int eventId)
    {
        var cart = GetCart();
        cart.Items.RemoveAll(i => i.EventId == eventId);
        SaveCart(cart);
    }

    public void ClearCart()
    {
        Session.Remove(CartSessionKey);
    }

    public int GetCartItemCount()
    {
        return GetCart().TotalItems;
    }
}

