using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Risk.Web.Data;

/// <summary>
/// The app's only <see cref="DbContext"/>. PR1 wires up Identity's stock
/// tables only (<c>AspNetUsers</c> etc.) — the <c>SavedGames</c> table
/// (design's save/resume schema) lands in a later migration once
/// <c>Risk.Web.Persistence</c> exists (design phase 5).
/// </summary>
public sealed class RiskDbContext(DbContextOptions<RiskDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
