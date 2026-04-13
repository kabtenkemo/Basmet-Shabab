using System.Security.Claims;

namespace BasmaApi.Services;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetMemberId(this ClaimsPrincipal principal)
    {
        var memberIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : null;
    }
}