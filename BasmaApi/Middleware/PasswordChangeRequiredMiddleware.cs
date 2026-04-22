using BasmaApi.Data;
using BasmaApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

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

        bool mustChangePassword;
        try
        {
            mustChangePassword = await dbContext.Members
                .AsNoTracking()
                .Where(member => member.Id == memberId.Value)
                .Select(member => member.MustChangePassword)
                .FirstOrDefaultAsync(context.RequestAborted);
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex,
                "Password-change middleware detected schema mismatch. Attempting lightweight repair and continuing request. MemberId={MemberId}",
                memberId.Value);

            try
            {
                dbContext.Database.ExecuteSqlRaw(
                    "IF COL_LENGTH('dbo.Members', 'MustChangePassword') IS NULL ALTER TABLE dbo.Members ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Members_MustChangePassword DEFAULT 0;");
            }
            catch (SqlException repairEx)
            {
                _logger.LogWarning(repairEx,
                    "Password-change middleware could not repair schema automatically. Continuing without password-change enforcement for this request. MemberId={MemberId}",
                    memberId.Value);
            }

            await _next(context);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Password-change middleware failed unexpectedly. Returning 503 instead of 500. MemberId={MemberId}",
                memberId.Value);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "تعذر التحقق من حالة كلمة المرور حاليًا. يرجى المحاولة بعد قليل.",
                traceId = context.TraceIdentifier
            }, context.RequestAborted);
            return;
        }

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