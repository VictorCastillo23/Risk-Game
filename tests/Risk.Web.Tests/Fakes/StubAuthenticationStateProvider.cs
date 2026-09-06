using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Risk.Web.Tests.Fakes;

/// <summary>
/// Test double for <see cref="AuthenticationStateProvider"/> (design D4:
/// <c>GameSessionService</c> depends on this abstract framework type, never
/// <c>UserManager&lt;T&gt;</c>, precisely so it stays this trivially
/// fakeable). <see cref="Anonymous"/>/<see cref="SignedIn"/> factory methods
/// cover both auth states <c>GameSessionService</c> tests need to simulate,
/// with no real Identity/DB dependency.
/// </summary>
internal sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    private StubAuthenticationStateProvider(ClaimsPrincipal principal)
    {
        _state = new AuthenticationState(principal);
    }

    public static StubAuthenticationStateProvider Anonymous() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public static StubAuthenticationStateProvider SignedIn(string userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            authenticationType: "Test");
        return new StubAuthenticationStateProvider(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(_state);
}
