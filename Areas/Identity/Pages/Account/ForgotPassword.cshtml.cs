using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Assignment1.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Areas.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;

    public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
    {
        _userManager = userManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var normalizedEmail = _userManager.NormalizeEmail(Input.Email);
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            return RedirectToPage("./ForgotPasswordConfirmation");

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "./ResetPassword",
            pageHandler: null,
            values: new { area = "Identity", code, email = Input.Email },
            protocol: Request.Scheme);

        await _emailSender.SendEmailAsync(
            Input.Email,
            "Reset your password",
            $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl ?? string.Empty)}'>clicking here</a>.");

        return RedirectToPage("./ForgotPasswordConfirmation");
    }
}

