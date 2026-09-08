namespace Risk.Web.RateLimiting;

/// <summary>
/// Configurable thresholds for the fixed-window rate limiter applied to the
/// POST handlers of <c>/Account/Register</c> and <c>/Account/Login</c> (see
/// <see cref="RateLimitPolicies.AuthEndpoints"/>). Bound from configuration
/// section <see cref="SectionName"/>, so a test host can override the
/// defaults (via <c>ConfigureAppConfiguration</c>) to use a small,
/// deterministic threshold instead of waiting out the production window.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:AuthEndpoints";

    /// <summary>
    /// Requests allowed per client (partitioned by IP) within
    /// <see cref="WindowSeconds"/>. Five per minute is generous enough for a
    /// legitimate user who mistypes a password or retries a signup, while
    /// still blocking the "new account per request" / "many different
    /// emails" abuse pattern that Identity's own account-lockout policy
    /// (keyed on one existing account) does not cover.
    /// </summary>
    public int PermitLimit { get; set; } = 5;

    public int WindowSeconds { get; set; } = 60;
}
