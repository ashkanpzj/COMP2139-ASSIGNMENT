using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Assignment1.ViewModels;

public class DashboardViewModel
{
    public IReadOnlyList<MyTicketCard> UpcomingTickets { get; init; } = Array.Empty<MyTicketCard>();
    public int TicketsPage { get; init; }
    public int TicketsTotalPages { get; init; }
    public IReadOnlyList<PurchaseHistoryCard> PurchaseHistory { get; init; } = Array.Empty<PurchaseHistoryCard>();
    public IReadOnlyList<MyEventCard> MyEvents { get; init; } = Array.Empty<MyEventCard>();
    public ProfileForm Profile { get; init; } = new();
    public bool ShowOrganizerPanel => MyEvents.Count > 0;
}

public record MyTicketCard(
    int PurchaseId,
    string EventTitle,
    DateTime EventDateLocal,
    int Quantity,
    decimal TotalPrice,
    string QrCodeDataUrl
);

public record PurchaseHistoryCard(
    int EventId,
    string EventTitle,
    DateTime EventDateLocal,
    int Quantity,
    decimal TotalPrice,
    int? Rating
);

public record MyEventCard(
    int EventId,
    string EventTitle,
    DateTime EventDateLocal,
    decimal TicketPrice,
    int TicketsSold,
    decimal TotalRevenue
);

public class ProfileForm
{
    [Display(Name = "Full name")]
    [StringLength(100)]
    public string? FullName { get; set; }

    [Display(Name = "Phone number")]
    [StringLength(20)]
    [RegularExpression(@"^[\d\s\-\+\(\)]*$", ErrorMessage = "Please enter a valid phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Profile photo")]
    public IFormFile? ProfileImage { get; set; }

    [BindNever]
    public string? CurrentImageUrl { get; set; }

    [BindNever]
    public string? UserEmail { get; set; }
}


