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
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
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

        // Unit numbers only need to be unique within a property (e.g. two different
        // buildings can both have a unit "101").
        builder.Entity<Unit>()
            .HasIndex(u => new { u.PropertyId, u.UnitNumber })
            .IsUnique();

        // Delete policy: every FK into a business/audit record (Property, UnitType, Unit,
        // AspNetUsers) is Restrict, so removing a "parent" row can never silently wipe out
        // units, applications, review history, or leases that reference it — the app must
        // reject the delete with a clear message instead. Only RentalApplication's own detail
        // rows (ResidenceHistory, ApplicationStatusHistory, Lease) cascade with it, since those
        // are genuinely owned by the application and meaningless without it.
        builder.Entity<Unit>()
            .HasOne(u => u.Property)
            .WithMany(p => p.Units)
            .HasForeignKey(u => u.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Unit>()
            .HasOne(u => u.UnitType)
            .WithMany(t => t.Units)
            .HasForeignKey(u => u.UnitTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentalApplication>()
            .HasOne(a => a.Unit)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentalApplication>()
            .HasOne(a => a.Applicant)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Lease>()
            .HasOne(l => l.Unit)
            .WithMany(u => u.Leases)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApplicationStatusHistory>()
            .HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One application can produce at most one lease — enforced via a unique index on
        // the FK, not just app logic.
        builder.Entity<Lease>()
            .HasOne(l => l.RentalApplication)
            .WithOne(a => a.Lease)
            .HasForeignKey<Lease>(l => l.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
