// Silence nullable literal warnings in test setup
#pragma warning disable CS8625
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Assignment1.Controllers;
using Assignment1.Data;
using Assignment1.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Assignment1.Tests;

public class DashboardControllerTests
{
    private static (DashboardController ctrl, ApplicationDbContext db) BuildController(
        ApplicationUser? user,
        Action<Mock<UserManager<ApplicationUser>>>? setupUserManager = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);

        var userStore = Mock.Of<IUserStore<ApplicationUser>>();
        var um = new Mock<UserManager<ApplicationUser>>(userStore, null, null, null, null, null, null, null, null);
        um.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        setupUserManager?.Invoke(um);

        var env = Mock.Of<IWebHostEnvironment>();
        var logger = Mock.Of<ILogger<DashboardController>>();

        var ctrl = new DashboardController(db, um.Object, env, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = BuildPrincipal(user) }
            }
        };

        return (ctrl, db);
    }

    [Fact]
    public async Task UpdateRating_Allows_First_Rating_When_Purchased()
    {
        var user = new ApplicationUser { Id = "u1", Email = "u1@test.com" };
        var (ctrl, db) = BuildController(user);

        db.Events.Add(new Event { EventId = 10, Title = "E", Date = DateTime.UtcNow.AddDays(1), AvailableTickets = 10 });
        db.TicketPurchases.Add(new TicketPurchase { EventId = 10, BuyerUserId = user.Id, Quantity = 1, UnitPrice = 10, TotalPrice = 10 });
        await db.SaveChangesAsync();

        var result = await ctrl.UpdateRating(10, 5) as JsonResult;

        Assert.NotNull(result);
        var success = GetJsonProperty<bool>(result!, "success");
        Assert.True(success);
        Assert.Equal(1, await db.EventRatings.CountAsync());
    }

    [Fact]
    public async Task UpdateRating_Blocks_When_Not_Purchased()
    {
        var user = new ApplicationUser { Id = "u2", Email = "u2@test.com" };
        var (ctrl, db) = BuildController(user);

        db.Events.Add(new Event { EventId = 11, Title = "E2", Date = DateTime.UtcNow.AddDays(1), AvailableTickets = 10 });
        await db.SaveChangesAsync();

        var result = await ctrl.UpdateRating(11, 4) as JsonResult;

        Assert.NotNull(result);
        var success = GetJsonProperty<bool>(result!, "success");
        var message = GetJsonProperty<string>(result!, "message");
        Assert.False(success);
        Assert.Equal("You can only rate events you've attended", message);
        Assert.Equal(0, await db.EventRatings.CountAsync());
    }

    [Fact]
    public async Task UpdateRating_Blocks_Duplicate_Rating()
    {
        var user = new ApplicationUser { Id = "u3", Email = "u3@test.com" };
        var (ctrl, db) = BuildController(user);

        db.Events.Add(new Event { EventId = 12, Title = "E3", Date = DateTime.UtcNow.AddDays(1), AvailableTickets = 10 });
        db.TicketPurchases.Add(new TicketPurchase { EventId = 12, BuyerUserId = user.Id, Quantity = 1, UnitPrice = 10, TotalPrice = 10 });
        db.EventRatings.Add(new EventRating { EventId = 12, UserId = user.Id, Rating = 5 });
        await db.SaveChangesAsync();

        var result = await ctrl.UpdateRating(12, 3) as JsonResult;

        Assert.NotNull(result);
        var success = GetJsonProperty<bool>(result!, "success");
        var message = GetJsonProperty<string>(result!, "message");
        Assert.False(success);
        Assert.Equal("You have already rated this event", message);
        Assert.Equal(1, await db.EventRatings.CountAsync()); // unchanged
    }

    [Fact]
    public async Task Index_Returns_Challenge_When_No_User()
    {
        var (ctrl, _) = BuildController(user: null);

        var result = await ctrl.Index();

        Assert.IsType<ChallengeResult>(result);
    }

    private static ClaimsPrincipal BuildPrincipal(ApplicationUser? user)
    {
        if (user == null) return new ClaimsPrincipal(new ClaimsIdentity());
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Email ?? user.Id)
        }, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static T GetJsonProperty<T>(JsonResult result, string prop)
    {
        var val = result.Value!;
        var pi = val.GetType().GetProperty(prop);
        Assert.NotNull(pi);
        return (T)pi!.GetValue(val)!;
    }
}

