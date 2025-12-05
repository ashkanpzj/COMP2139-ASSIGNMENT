using Assignment1.Controllers;
using Assignment1.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Assignment1.Tests;

public class HomeControllerTests
{
    private static HomeController CreateController()
    {
        var logger = Mock.Of<ILogger<HomeController>>();
        return new HomeController(logger);
    }

    [Fact]
    public void Search_Tickets_Kind_Redirects_To_Tickets_Index_With_Query()
    {
        var controller = CreateController();

        var result = controller.Search("rock", "tickets") as RedirectToActionResult;

        Assert.NotNull(result);
        Assert.Equal("Index", result!.ActionName);
        Assert.Equal("Tickets", result.ControllerName);
        Assert.Equal("rock", result.RouteValues?["search"]);
    }

    [Fact]
    public void Search_Defaults_To_Events_When_Kind_Null_Or_Unknown()
    {
        var controller = CreateController();

        var result = controller.Search("jazz", null) as RedirectToActionResult;

        Assert.NotNull(result);
        Assert.Equal("Index", result!.ActionName);
        Assert.Equal("Events", result.ControllerName);
        Assert.Equal("jazz", result.RouteValues?["search"]);
    }

    [Fact]
    public void Error_Returns_View_With_ErrorViewModel()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var result = controller.Error() as ViewResult;

        Assert.NotNull(result);
        Assert.IsType<ErrorViewModel>(result!.Model);
    }
}

