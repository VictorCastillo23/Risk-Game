using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Risk.Web.Data;

/// <summary>
/// The app's only <see cref="DbContext"/>. Wires up Identity's stock tables
/// (<c>AspNetUsers</c> etc.) plus the <c>SavedGames</c> table (PR5, design's
/// save/resume schema).
/// </summary>
public sealed class RiskDbContext(DbContextOptions<RiskDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<SavedGame> SavedGames => Set<SavedGame>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SavedGame>(entity =>
        {
            // Shared-primary-key one-to-one: OwnerId IS the FK to
            // AspNetUsers.Id, cascade delete — deleting a user deletes their
            // save (design's "PK is the FK" schema note).
            entity.HasKey(sg => sg.OwnerId);
            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<SavedGame>(sg => sg.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(sg => sg.Mode).HasMaxLength(32);
            entity.Property(sg => sg.Phase).HasMaxLength(32);
        });
    }
}
