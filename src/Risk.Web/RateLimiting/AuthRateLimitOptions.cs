namespace Risk.Web.RateLimiting;

/// <summary>
/// Configurable thresholds for the fixed-window rate limiter applied to the
/// POST handlers of <c>/Account/Register</c> and <c>/Account/Login</c> (see
/// <see cref="RateLimitPolicies.AuthEndpoints"/>). Bound from configuration
/// section <see cref="SectionName"/>, so a test host can override the
/// defaults (via <c>ConfigureAppConfiguration</c>) to use a small,
/// deterministic threshold instead of waiting out the production window.
///
/// <para>
/// Canonical rationale (other references to "why this rate limiter exists"
/// in this codebase — <c>Program.cs</c>, <c>RateLimitingTests.cs</c> — point
/// back here rather than repeating it): this is a distinct hardening layer
/// from <c>IdentityOptions.Lockout</c>. Lockout only protects one *existing*
/// account from repeated wrong-password guesses. This limiter instead stops
/// a single client from spamming brand-new registrations or login attempts
/// across many different emails — an abuse pattern lockout does not cover,
/// and one that would otherwise keep the serverless Azure SQL Database
/// billing indefinitely (it only auto-pauses after 15 minutes idle).
/// </para>
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:AuthEndpoints";

    /// <summary>
    /// Requests allowed per client (partitioned by IP) within
    /// <see cref="WindowSeconds"/>. Five per minute is generous enough for a
    /// legitimate user who mistypes a password or retries a signup — see
    /// the rationale on <see cref="AuthRateLimitOptions"/> itself for why
    /// this exists as a layer distinct from Identity's account lockout.
    /// </summary>
    public int PermitLimit { get; set; } = 5;

    public int WindowSeconds { get; set; } = 60;
}
