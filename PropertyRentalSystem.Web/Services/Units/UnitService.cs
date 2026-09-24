using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Units;

public class UnitService : IUnitService
{
    private readonly ApplicationDbContext _db;

    public UnitService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit?> GetByIdAsync(int id) => await _db.Units.FindAsync(id);

    public async Task<bool> PropertyExistsAsync(int propertyId) =>
        await _db.Properties.AnyAsync(p => p.Id == propertyId);

    public async Task<List<UnitType>> GetSelectableUnitTypesAsync(int? currentUnitTypeId) =>
        await _db.UnitTypes
            .Where(t => t.IsActive || t.Id == currentUnitTypeId)
            .OrderBy(t => t.Name)
            .ToListAsync();

    public async Task<ServiceResult<Unit>> CreateAsync(int propertyId, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId)
    {
        var errors = await ValidateAsync(propertyId, unitNumber, unitTypeId, currentUnitTypeId: null, currentUnitId: null);
        if (errors.Count > 0)
            return ServiceResult<Unit>.Fail(errors);

        var unit = new Unit
        {
            PropertyId = propertyId,
            UnitNumber = unitNumber,
            Bedrooms = bedrooms,
            MonthlyRent = monthlyRent,
            UnitTypeId = unitTypeId
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync();
        return ServiceResult<Unit>.Success(unit);
    }

    public async Task<ServiceResult> UpdateAsync(Unit unit, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId)
    {
        var errors = await ValidateAsync(unit.PropertyId, unitNumber, unitTypeId, currentUnitTypeId: unit.UnitTypeId, currentUnitId: unit.Id);
        if (errors.Count > 0)
            return ServiceResult.Fail(errors);

        unit.UnitNumber = unitNumber;
        unit.Bedrooms = bedrooms;
        unit.MonthlyRent = monthlyRent;
        unit.UnitTypeId = unitTypeId;
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }

    // A unit with applications or a lease against it can't be removed — checked proactively,
    // same reasoning as PropertyService.DeleteAsync.
    public async Task<ServiceResult> DeleteAsync(Unit unit)
    {
        var hasApplications = await _db.RentalApplications.AnyAsync(a => a.UnitId == unit.Id);
        var hasLease = await _db.Leases.AnyAsync(l => l.UnitId == unit.Id);
        if (hasApplications || hasLease)
            return ServiceResult.Fail("Can't delete a unit that has applications or a lease against it.");

        _db.Units.Remove(unit);
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }

    // Enforced on the server: an inactive unit type may stay on the unit that already has it,
    // but can never be (re)selected — neither for a different unit, nor as a new choice when
    // editing this one. Field names match UnitFormViewModel's property names by convention.
    private async Task<List<ServiceError>> ValidateAsync(
        int propertyId, string unitNumber, int unitTypeId, int? currentUnitTypeId, int? currentUnitId)
    {
        var errors = new List<ServiceError>();

        if (!await PropertyExistsAsync(propertyId))
        {
            errors.Add(new ServiceError(string.Empty, "Property not found."));
            return errors;
        }

        var candidateIsActive = await _db.UnitTypes.AnyAsync(t => t.Id == unitTypeId && t.IsActive);
        if (!UnitTypeRules.CanAssign(candidateIsActive, unitTypeId, currentUnitTypeId))
            errors.Add(new ServiceError("UnitTypeId", "This unit type is inactive and can't be assigned."));

        var duplicateNumber = await _db.Units.AnyAsync(u =>
            u.PropertyId == propertyId &&
            u.UnitNumber == unitNumber &&
            (!currentUnitId.HasValue || u.Id != currentUnitId.Value));
        if (duplicateNumber)
            errors.Add(new ServiceError("UnitNumber", "This property already has a unit with this number."));

        return errors;
    }
}
