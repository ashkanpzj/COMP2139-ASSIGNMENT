using System;
using System.ComponentModel.DataAnnotations;

namespace Assignment1.Models
{
    public class Event
    {
        public int EventId { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [DataType(DataType.DateTime)]
        public DateTime Date { get; set; } = DateTime.UtcNow; 

        [StringLength(1000)]
        public string? Description { get; set; }

        [Range(0, 999999)]
        public decimal? Price { get; set; }

        [Display(Name = "Available Tickets")]
        [Range(0, int.MaxValue, ErrorMessage = "Ticket count must be positive.")]
        public int AvailableTickets { get; set; } = 0;

        [StringLength(100)]
        public string? Category { get; set; }
    }
}