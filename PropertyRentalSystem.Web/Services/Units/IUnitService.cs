using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services;

namespace PropertyRentalSystem.Web.Services.Units;

public interface IUnitService
{
    Task<Unit?> GetByIdAsync(int id);
    Task<bool> PropertyExistsAsync(int propertyId);

    // Active unit types plus, when editing, the unit's own current type even if it has since
    // gone inactive — so it still displays as an option (per the inactive-type rule).
    Task<List<UnitType>> GetSelectableUnitTypesAsync(int? currentUnitTypeId);

    Task<ServiceResult<Unit>> CreateAsync(int propertyId, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId);
    Task<ServiceResult> UpdateAsync(Unit unit, string unitNumber, int bedrooms, decimal monthlyRent, int unitTypeId);
    Task<ServiceResult> DeleteAsync(Unit unit);
}
