using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// The Register/Login/Logout Razor Pages sit behind
/// <c>app.UseAntiforgery()</c> (already wired in PR1's <c>Program.cs</c>),
/// so a real POST test needs a real, matching antiforgery token. This
/// mirrors what a browser does automatically: GET the page and read the
/// hidden <c>__RequestVerificationToken</c> field the form tag helper
/// emits. The matching antiforgery cookie doesn't need manual handling —
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions.HandleCookies"/>
/// defaults to <see langword="true"/>, so as long as callers reuse the same
/// <see cref="HttpClient"/> instance across a GET/POST pair, the client's
/// own cookie container carries it automatically (and the antiforgery
/// middleware only re-issues a Set-Cookie when no valid one is present yet,
/// so relying on one always being there would be wrong past the first
/// request).
/// </summary>
internal static class AntiForgeryTestHelper
{
    private static readonly Regex TokenInputTag = new(
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ValueAttribute = new(
        "value=\"([^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<string> GetTokenAsync(HttpClient client, string requestUri)
    {
        var response = await client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var tagMatch = TokenInputTag.Match(html);
        if (!tagMatch.Success)
        {
            throw new InvalidOperationException(
                $"No __RequestVerificationToken hidden field found in the response from {requestUri}.");
        }

        var valueMatch = ValueAttribute.Match(tagMatch.Value);
        if (!valueMatch.Success)
        {
            throw new InvalidOperationException(
                $"__RequestVerificationToken field had no value attribute in the response from {requestUri}.");
        }

        return valueMatch.Groups[1].Value;
    }

    /// <summary>
    /// Shared across Register/Login (and any future auth test) so both stop
    /// duplicating the same "does this response set a cookie whose header
    /// value contains this substring" check.
    /// </summary>
    public static bool HasSetCookieContaining(HttpResponseMessage response, string needle) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
        cookies.Any(c => c.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
