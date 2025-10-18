namespace Assignment1.Models
{
    public class Ticket
    {
        public int TicketId { get; set; }   
        public int EventId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}