// Silence nullable literal warnings in mocked dependencies
#pragma warning disable CS8625
using System.Linq;
using System.Threading.Tasks;
using Assignment1.Controllers;
using Assignment1.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Assignment1.Tests;

public class AdminControllerTests
{
    [Fact]
    public void AdminController_Has_Authorize_Admin_Role()
    {
        var attr = typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("Admin", attr!.Roles);
    }

    [Fact]
    public async Task MakeAdmin_Adds_Admin_Role_When_Not_Already_Admin()
    {
        var user = new ApplicationUser { Id = "user1", Email = "user1@test.com" };

        var um = MockUserManager();
        um.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        um.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);
        um.Setup(x => x.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

        var rm = MockRoleManager();
        rm.Setup(x => x.RoleExistsAsync("Admin")).ReturnsAsync(true);

        var ctrl = CreateController(um.Object, rm.Object);

        var result = await ctrl.MakeAdmin(user.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Users), redirect.ActionName);
        um.Verify(x => x.AddToRoleAsync(user, "Admin"), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_Confirms_When_Not_Confirmed()
    {
        var user = new ApplicationUser { Id = "user2", Email = "user2@test.com", EmailConfirmed = false };

        var um = MockUserManager();
        um.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        um.Setup(x => x.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("token");
        um.Setup(x => x.ConfirmEmailAsync(user, "token")).ReturnsAsync(IdentityResult.Success);

        var ctrl = CreateController(um.Object, MockRoleManager().Object);

        var result = await ctrl.ConfirmEmail(user.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Users), redirect.ActionName);
        um.Verify(x => x.ConfirmEmailAsync(user, "token"), Times.Once);
    }

    private static AdminController CreateController(UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm)
    {
        var logger = Mock.Of<ILogger<AdminController>>();
        var ctx = new DefaultHttpContext();
        var tempData = new TempDataDictionary(ctx, Mock.Of<ITempDataProvider>());

        return new AdminController(um, null!, rm, logger)
        {
            TempData = tempData
        };
    }

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = Mock.Of<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
    }

    private static Mock<RoleManager<IdentityRole>> MockRoleManager()
    {
        var store = Mock.Of<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(store, null, null, null, null);
    }
}

