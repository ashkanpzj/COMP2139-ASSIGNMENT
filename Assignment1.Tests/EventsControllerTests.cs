// Silence nullable literal warnings in test inputs
#pragma warning disable CS8625
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assignment1.Controllers;
using Assignment1.Data;
using Assignment1.Models;
using Assignment1.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Assignment1.Tests;

public class EventsControllerTests
{
    [Fact]
    public async Task LiveSearch_Prioritizes_LowStock_Then_Upcoming_Then_Past()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);
        var now = DateTime.UtcNow;

        db.Events.AddRange(
            new Event { EventId = 1, Title = "NormalUpcoming", Date = now.AddDays(2), AvailableTickets = 50, Category = "Concert" },
            new Event { EventId = 2, Title = "LowStock", Date = now.AddDays(1), AvailableTickets = 2, Category = "Concert" },
            new Event { EventId = 3, Title = "PastEvent", Date = now.AddDays(-1), AvailableTickets = 100, Category = "Concert" }
        );
        await db.SaveChangesAsync();

        var controller = new EventsController(
            db,
            MockUserManager(),
            MockSignInManager(),
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IWebHostEnvironment>());

        // Act
        var result = await controller.LiveSearch(
            search: null!,
            category: null!,
            startDate: null!,
            endDate: null!,
            sort: "date") as PartialViewResult;

        // Assert
        Assert.NotNull(result);
        var items = Assert.IsType<List<EventCardViewModel>>(result!.Model);
        Assert.Collection(items,
            i => Assert.Equal("LowStock", i.Title),
            i => Assert.Equal("NormalUpcoming", i.Title),
            i => Assert.Equal("PastEvent", i.Title));
    }

    private static UserManager<ApplicationUser> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null).Object;
    }

    private static SignInManager<ApplicationUser> MockSignInManager()
    {
        var userManager = MockUserManager();
        var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        return new Mock<SignInManager<ApplicationUser>>(userManager, contextAccessor.Object, claimsFactory.Object, null, null, null, null).Object;
    }
}

