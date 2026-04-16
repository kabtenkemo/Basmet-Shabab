namespace BasmaApi.Services;

public sealed class BcryptPasswordService : IPasswordService
{
    private readonly ILogger<BcryptPasswordService> _logger;

    public BcryptPasswordService(ILogger<BcryptPasswordService> logger)
    {
        _logger = logger;
    }

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        // BCrypt with 12 rounds (cost factor) for strong password security
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException ex)
        {
            _logger.LogWarning(ex, "Password hash format is invalid.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while verifying password hash.");
            return false;
        }
    }
}