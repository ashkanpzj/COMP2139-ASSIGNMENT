using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Assignment1.ViewModels;

public class ProfileViewModel
{
    [Display(Name = "Full name")]
    [StringLength(100)]
    public string? FullName { get; set; }

    [Display(Name = "Phone number")]
    [StringLength(20)]
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

