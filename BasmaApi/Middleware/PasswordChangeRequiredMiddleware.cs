using BasmaApi.Data;
using BasmaApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Middleware;

public sealed class PasswordChangeRequiredMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PasswordChangeRequiredMiddleware> _logger;

    public PasswordChangeRequiredMiddleware(RequestDelegate next, ILogger<PasswordChangeRequiredMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (IsAllowedWithoutPasswordChange(context))
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

        _logger.LogInformation(
            "Request blocked until password change is completed for member {MemberId}. Method={Method} Path={Path}",
            memberId.Value,
            context.Request.Method,
            context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "يجب تغيير كلمة المرور أولًا." }, context.RequestAborted);
    }

    private static bool IsAllowedWithoutPasswordChange(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/members/me")
            || context.Request.Path.StartsWithSegments("/api/auth/change-password")
            || context.Request.Path.StartsWithSegments("/api/auth/logout"))
        {
            return true;
        }

        // Public governorates/committees reads are allowed before password change.
        return HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path.StartsWithSegments("/api/governorates");
    }
}