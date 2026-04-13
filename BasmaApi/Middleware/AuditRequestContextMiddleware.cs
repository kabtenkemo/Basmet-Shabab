using BasmaApi.Services;

namespace BasmaApi.Middleware;

public sealed class AuditRequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public AuditRequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditRequestContextAccessor accessor)
    {
        accessor.Set(AuditRequestContextAccessor.FromClaims(
            context.User,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Path,
            context.Request.Method));

        try
        {
            await _next(context);
        }
        finally
        {
            accessor.Set(null);
        }
    }
}