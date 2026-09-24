using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Applications;

public class ApplicationBrowseService : IApplicationBrowseService
{
    private readonly ApplicationDbContext _db;

    public ApplicationBrowseService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Unit>> GetAvailableUnitsAsync(int? propertyId, int? unitTypeId, int? bedrooms, decimal? maxRent)
    {
        var today = DateTime.UtcNow.Date;

        // Narrow to not-yet-expired leases in SQL, then apply the exact "covers today" rule
        // in memory so the same LeaseRules.CoversDate logic is what gets unit-tested.
        var unavailableUnitIds = (await _db.Leases.Where(l => l.EndDate >= today).ToListAsync())
            .Where(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today))
            .Select(l => l.UnitId)
            .ToHashSet();

        var query = _db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Where(u => !unavailableUnitIds.Contains(u.Id));

        if (propertyId.HasValue)
            query = query.Where(u => u.PropertyId == propertyId.Value);
        if (unitTypeId.HasValue)
            query = query.Where(u => u.UnitTypeId == unitTypeId.Value);
        if (bedrooms.HasValue)
            query = query.Where(u => u.Bedrooms == bedrooms.Value);
        if (maxRent.HasValue)
            query = query.Where(u => u.MonthlyRent <= maxRent.Value);

        return await query.OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber).ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetOpenApplicationUnitMapAsync(string applicantId) =>
        await _db.RentalApplications
            .Where(a => a.ApplicantId == applicantId && RentalApplicationRules.OpenStatuses.Contains(a.Status))
            .ToDictionaryAsync(a => a.UnitId, a => a.Id);

    public async Task<Unit?> GetUnitAsync(int unitId) => await _db.Units.FindAsync(unitId);

    public async Task<List<UnitType>> GetUnitTypesAsync() =>
        await _db.UnitTypes.OrderBy(t => t.Name).ToListAsync();

    public async Task<List<int>> GetDistinctBedroomCountsAsync() =>
        await _db.Units.Select(u => u.Bedrooms).Distinct().OrderBy(b => b).ToListAsync();

    public async Task<ServiceResult<RentalApplication>> StartApplicationAsync(Unit unit, string applicantId, string? applicantEmail)
    {
        var today = DateTime.UtcNow.Date;
        var unitLeases = await _db.Leases.Where(l => l.UnitId == unit.Id).ToListAsync();
        if (unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today)))
            return ServiceResult<RentalApplication>.Fail("This unit is no longer available.");

        var existing = await _db.RentalApplications.FirstOrDefaultAsync(a =>
            a.ApplicantId == applicantId && a.UnitId == unit.Id && RentalApplicationRules.OpenStatuses.Contains(a.Status));
        if (existing != null)
            return ServiceResult<RentalApplication>.Success(existing);

        var application = new RentalApplication
        {
            ApplicantId = applicantId,
            UnitId = unit.Id,
            Status = ApplicationStatus.Draft,
            Email = applicantEmail ?? string.Empty
        };
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = ApplicationStatus.Draft,
            ChangedByUserId = applicantId,
            ChangedAt = DateTime.UtcNow
        });

        _db.RentalApplications.Add(application);
        await _db.SaveChangesAsync();
        return ServiceResult<RentalApplication>.Success(application);
    }
}
