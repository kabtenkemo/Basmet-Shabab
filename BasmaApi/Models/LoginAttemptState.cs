namespace BasmaApi.Models;

/// <summary>
/// Tracks login attempt state for rate limiting.
/// Used by in-memory concurrent rate limiting (similar to Gym-system-Api).
/// </summary>
public sealed class LoginAttemptState
{
    /// <summary>Number of failed attempts in the current window.</summary>
    public int Count { get; set; }

    /// <summary>Start time of the current rate limiting window (UTC).</summary>
    public DateTime WindowStartUtc { get; set; }
}
