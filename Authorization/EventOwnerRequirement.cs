using Assignment1.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Assignment1.Authorization;

public class EventOwnerRequirement : IAuthorizationRequirement;

public class EventOwnerHandler : AuthorizationHandler<EventOwnerRequirement, Event>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EventOwnerRequirement requirement, Event resource)
    {
        if (context.User.IsInRole(RoleNames.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(resource.CreatedByUserId) &&
            !string.IsNullOrEmpty(userId) &&
            resource.CreatedByUserId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}






