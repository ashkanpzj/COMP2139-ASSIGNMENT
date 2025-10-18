using System;
using System.ComponentModel.DataAnnotations;

namespace Assignment1.Models
{
    public class TicketPurchase
    {
        public int TicketPurchaseId { get; set; }

        [Required]
        public int EventId { get; set; }
        public Event? Event { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, 999999)]
        public decimal UnitPrice { get; set; }

        [Range(0, 999999)]
        public decimal TotalPrice { get; set; }
        
        public DateTime PurchasedAtUtc { get; set; } = DateTime.UtcNow;
        
        public string? BuyerUserId { get; set; }
        
        [StringLength(100)]
        public string? GuestFirstName { get; set; }

        [StringLength(100)]
        public string? GuestLastName { get; set; }

        [EmailAddress, StringLength(256)]
        public string? GuestEmail { get; set; }
    }
}