using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Assignment1.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public new string? PhoneNumber
    {
        get => base.PhoneNumber;
        set => base.PhoneNumber = value;
    }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(256)]
    public string? ProfilePictureUrl { get; set; }
}

