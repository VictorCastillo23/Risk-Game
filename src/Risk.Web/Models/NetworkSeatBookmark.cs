using Risk.Domain.Players;

namespace Risk.Web.Models;

/// <summary>
/// The (code, seat) a device persists in browser session storage after a
/// successful create/join, so a reload or reopened tab can rebind the same
/// seat instead of claiming a duplicate. Plain DTO for
/// <c>ProtectedSessionStorage</c> (System.Text.Json round-trips the
/// positional record). Read only inside <c>OnAfterRenderAsync</c> — never
/// during prerender, where JS interop throws.
/// </summary>
public sealed record NetworkSeatBookmark(string Code, int Seat)
{
    public const string StorageKey = "risk-netseat";

    public static NetworkSeatBookmark For(JoinCode code, PlayerId seat) =>
        new(code.Value, seat.Value);
}
