namespace Assignment1.ViewModels;

public class EventCardViewModel
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public int AvailableTickets { get; set; }
    public string? ImageUrl { get; set; }
    public string? CreatedByUserId { get; set; }
    
    // Rating info
    public double AverageRating { get; set; }
    public int TotalRatings { get; set; }
    
    public bool IsSoldOut => AvailableTickets <= 0;
    public bool IsPastEvent => Date < DateTime.UtcNow;
    // Only show low stock for upcoming events with less than 5 tickets
    public bool IsLowStock => !IsPastEvent && AvailableTickets > 0 && AvailableTickets < 5;
}

