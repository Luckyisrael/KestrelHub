using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Middleware;

public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is not null)
            {
                var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
                var user = await dbContext.Users.FindAsync(userId);

                if (user is null || !user.IsActive)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { Error = "Account is deactivated." });
                    return;
                }
            }
        }

        await _next(context);
    }
}
