using System.Text;
using Assignment1.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Assignment1.Areas.Identity.Pages.Account;

public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public bool Successful { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? returnUrl = null)
    {
        if (userId == null || code == null)
            return RedirectToPage("./Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound($"Unable to load user with ID '{userId}'.");

        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
        Successful = result.Succeeded;

        if (Successful && !string.IsNullOrWhiteSpace(returnUrl))
            return LocalRedirect(returnUrl);

        return Page();
    }
}



