using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment1.Models;

public class EventComment
{
    public int EventCommentId { get; set; }
    
    [Required]
    public int EventId { get; set; }
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    public string? UserId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    // For guest comments
    public string? GuestName { get; set; }
    
    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    // Computed property for display name
    [NotMapped]
    public string DisplayName => User?.FullName ?? GuestName ?? "Anonymous";
}




