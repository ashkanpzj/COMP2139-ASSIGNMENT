using Microsoft.AspNetCore.Mvc;

namespace Assignment1.Controllers;

public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    [Route("Error/{statusCode}")]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        _logger.LogWarning("HTTP {StatusCode} error occurred. Path: {Path}", 
            statusCode, HttpContext.Request.Path);

        return statusCode switch
        {
            404 => View("NotFound"),
            500 => View("ServerError"),
            _ => View("GenericError", statusCode)
        };
    }

    [Route("Error/500")]
    public IActionResult ServerError()
    {
        _logger.LogError("Internal server error occurred");
        return View("ServerError");
    }
}



