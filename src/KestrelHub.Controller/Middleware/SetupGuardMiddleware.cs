using KestrelHub.Controller.Data;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Middleware;

public class SetupGuardMiddleware
{
    private readonly RequestDelegate _next;

    public SetupGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Always allow static files and Blazor framework files
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Always allow setup status endpoint
        if (path.StartsWith("/api/setup/status", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check if setup is complete
        var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
        var settings = await dbContext.SystemSettings.FirstOrDefaultAsync();

        if (settings is null || !settings.IsSetupComplete)
        {
            // Setup not complete — only allow setup endpoints
            if (path.StartsWith("/api/setup", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Block all other API requests
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { Error = "Setup not complete. Please visit /api/setup/status." });
            return;
        }

        // Setup complete — block setup endpoints except status
        if (path.StartsWith("/api/setup", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/api/setup/status", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 404;
            return;
        }

        await _next(context);
    }
}
