namespace Assignment1.Models;

public class CartItem
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int AvailableTickets { get; set; }
    
    public decimal TotalPrice => UnitPrice * Quantity;
}

public class ShoppingCart
{
    public List<CartItem> Items { get; set; } = new();
    
    public int TotalItems => Items.Sum(i => i.Quantity);
    public decimal TotalPrice => Items.Sum(i => i.TotalPrice);
}

