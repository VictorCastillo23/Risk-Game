using Microsoft.AspNetCore.Identity;

namespace Risk.Web.Data;

/// <summary>
/// The Identity user for this app. No extra profile fields yet — accounts
/// exist solely to own at most one <c>SavedGame</c> row (design D6/PR5), so
/// this stays the stock <see cref="IdentityUser"/> shape until a real need
/// for more surfaces.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
}
