using BasmaApi.Data;
using BasmaApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Middleware;

public sealed class PasswordChangeRequiredMiddleware
{
    // Only allow endpoints that don't require changed password
    // FIX: Removed /api/members to prevent circumventing password change requirement
    private static readonly string[] AllowedPaths =
    [
        "/api/members/me",
        "/api/auth/change-password",
        "/api/governorates",
        "/api/auth/logout"
    ];

    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (AllowedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await _next(context);
            return;
        }

        var memberId = context.User.GetMemberId();
        if (memberId is null)
        {
            await _next(context);
            return;
        }

        var mustChangePassword = await dbContext.Members
            .AsNoTracking()
            .Where(member => member.Id == memberId.Value)
            .Select(member => member.MustChangePassword)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (!mustChangePassword)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "يجب تغيير كلمة المرور أولًا." }, context.RequestAborted);
    }
}