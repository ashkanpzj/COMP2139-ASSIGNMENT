#nullable disable
using System.ComponentModel.DataAnnotations;
using Assignment1.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Email or username")]
            public string Identifier { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                ModelState.AddModelError(string.Empty, ErrorMessage);

            returnUrl ??= Url.Content("~/");
            
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var lookup = Input.Identifier?.Trim() ?? "";
                string username = lookup;
                ApplicationUser user = null;

                if (!string.IsNullOrEmpty(lookup))
                {
                    if (lookup.Contains("@", StringComparison.Ordinal))
                    {
                        var normalizedEmail = _userManager.NormalizeEmail(lookup);
                        user = await _userManager.Users
                            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
                    }

                    if (user == null)
                        user = await _userManager.FindByNameAsync(lookup);

                    username = user?.UserName ?? lookup;
                }

                var result = await _signInManager.PasswordSignInAsync(
                    username, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} logged in successfully from IP {IP}", Input.Identifier, ip);
                    return LocalRedirect(returnUrl);
                }

                if (result.RequiresTwoFactor)
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User {Email} account locked out from IP {IP}", Input.Identifier, ip);
                    return RedirectToPage("./Lockout");
                }

                if (result.IsNotAllowed)
                {
                    _logger.LogWarning("User {Email} login not allowed (email not confirmed) from IP {IP}", Input.Identifier, ip);
                    ModelState.AddModelError(string.Empty, "You must confirm your email before logging in.");
                    return Page();
                }

                _logger.LogWarning("User {Email} failed login from IP {IP}", Input.Identifier, ip);
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            return Page();
        }
    }
}
