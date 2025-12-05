using Assignment1.Data;
using Assignment1.Models;
using Assignment1.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment1.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _context = context;
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();

        var rows = new List<AdminUserRow>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(new AdminUserRow(
                user.Id,
                user.Email ?? "",
                user.EmailConfirmed,
                user.FullName ?? "",
                user.PhoneNumber,
                roles.ToArray()
            ));
        }

        var vm = new AdminUsersVm(rows);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmail(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["StatusMessage"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        if (user.EmailConfirmed)
        {
            TempData["StatusMessage"] = "Email is already confirmed.";
            return RedirectToAction(nameof(Users));
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await _userManager.ConfirmEmailAsync(user, token);

        TempData["StatusMessage"] = result.Succeeded
            ? $"Email confirmed for {user.Email}."
            : $"Failed to confirm email for {user.Email}.";

        if (!result.Succeeded)
        {
            _logger.LogWarning("Admin failed to confirm email for user {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["StatusMessage"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        if (!await _roleManager.RoleExistsAsync("Admin"))
        {
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["StatusMessage"] = $"{user.Email} is already an admin.";
            return RedirectToAction(nameof(Users));
        }

        var result = await _userManager.AddToRoleAsync(user, "Admin");
        TempData["StatusMessage"] = result.Succeeded
            ? $"{user.Email} is now an admin."
            : $"Failed to make {user.Email} admin.";

        if (!result.Succeeded)
        {
            _logger.LogWarning("Admin failed to add Admin role to user {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["StatusMessage"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        // prevent self-delete
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["StatusMessage"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Users));
        }

        var result = await _userManager.DeleteAsync(user);
        TempData["StatusMessage"] = result.Succeeded
            ? $"User {user.Email} deleted."
            : $"Failed to delete user {user.Email}.";

        if (!result.Succeeded)
        {
            _logger.LogWarning("Admin failed to delete user {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return RedirectToAction(nameof(Users));
    }
}


