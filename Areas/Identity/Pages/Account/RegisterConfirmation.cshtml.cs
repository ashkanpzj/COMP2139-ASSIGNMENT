using System.Text;
using Assignment1.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Areas.Identity.Pages.Account;

public class RegisterConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterConfirmationModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    public bool DisplayConfirmAccountLink { get; set; }

    public string? EmailConfirmationUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string? email = null, string? returnUrl = null)
    {
        if (email == null)
            return RedirectToPage("./Login");

        Email = email;

        // Use FirstOrDefaultAsync to avoid exception when duplicates exist
        var normalizedEmail = _userManager.NormalizeEmail(email);
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user == null)
            return NotFound($"Unable to load user with email '{email}'.");

#if DEBUG
        DisplayConfirmAccountLink = true;
#endif

        if (DisplayConfirmAccountLink)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            EmailConfirmationUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code, returnUrl },
                protocol: Request.Scheme);
        }

        return Page();
    }
}

