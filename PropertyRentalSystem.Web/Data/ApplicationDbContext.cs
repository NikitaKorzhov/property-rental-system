using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<ResidenceHistory> ResidenceHistories => Set<ResidenceHistory>();
    public DbSet<Lease> Leases => Set<Lease>();
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Unit>()
            .Property(u => u.MonthlyRent)
            .HasColumnType("decimal(18,2)");

        // Lease references both Unit and RentalApplication, and RentalApplication itself
        // cascades from Unit — by default that gives two cascade paths from Unit to Lease,
        // which SQL Server rejects. Make the direct Lease->Unit FK Restrict, leaving a
        // single cascade path via RentalApplication.
        builder.Entity<Lease>()
            .HasOne(l => l.Unit)
            .WithMany(u => u.Leases)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
