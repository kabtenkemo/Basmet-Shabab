using BasmaApi.Models;

namespace BasmaApi.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(Member member);
}