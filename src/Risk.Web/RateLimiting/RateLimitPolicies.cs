namespace Risk.Web.RateLimiting;

/// <summary>
/// Named rate-limiting policies registered against
/// <c>RateLimiterOptions</c> in <c>Program.cs</c> and referenced via
/// <c>[EnableRateLimiting]</c> on the Razor Page *classes* that should be
/// throttled — never on a handler method (e.g. <c>OnPostAsync</c>); see the
/// remarks on <see cref="AuthEndpoints"/> for why.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Registered on the <c>RegisterModel</c>/<c>LoginModel</c> page
    /// *classes* via <c>[EnableRateLimiting]</c> — not on their
    /// <c>OnPostAsync</c> handlers directly, because Razor Pages here
    /// exposes exactly one routing endpoint per page (GET/POST dispatch
    /// happens inside the page, not via distinct per-verb endpoints), so a
    /// handler-method-level attribute is silently ignored. The policy body
    /// itself (see <c>Program.cs</c>) inspects the request's HTTP method
    /// and only throttles POST, leaving the GET that renders the form
    /// unthrottled.
    /// </summary>
    public const string AuthEndpoints = "AuthEndpoints";
}
