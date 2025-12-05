using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment1.Models
{
    public class EventRating
    {
        public int EventRatingId { get; set; }

        [Required]
        public int EventId { get; set; }
        public Event? Event { get; set; }

        // For logged-in users
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // For guests (identified by email)
        [EmailAddress, StringLength(256)]
        public string? GuestEmail { get; set; }

        [Required, Range(1, 5)]
        public int Rating { get; set; }

        public DateTime RatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

