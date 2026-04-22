using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BasmaApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace BasmaApi.Services;

public sealed class JwtTokenService(IConfiguration configuration) : ITokenService
{
    public (string Token, DateTime ExpiresAtUtc) CreateToken(Member member)
    {
        var key = configuration["Jwt:Key"] ?? "dev-key-1234567890123456789012345678901234567890";
        var issuer = configuration["Jwt:Issuer"] ?? "basmet-shabab-dev";
        var audience = configuration["Jwt:Audience"] ?? "basmet-shabab-client-dev";
        var expiresMinutes = int.TryParse(configuration["Jwt:ExpiresMinutes"], out var parsedMinutes) ? parsedMinutes : 120;

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, member.Id.ToString()),
            new(ClaimTypes.Name, member.FullName),
            new(ClaimTypes.Email, member.Email),
            new(ClaimTypes.Role, member.Role.ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}