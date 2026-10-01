using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.ViewModels.Applications;

namespace PropertyRentalSystem.Web.Services.Applications;

public interface IApplicationBrowseService
{
    // "A unit whose lease term covers today is not available", optionally narrowed by
    // property/unit type/bedrooms/max rent — filtering is done in the database. Projected
    // straight to the ViewModel (Rule 9); ExistingApplicationId is left null here — the
    // caller composes it from GetOpenApplicationUnitMapAsync, since this method doesn't
    // know which applicant is browsing.
    Task<List<BrowseUnitViewModel>> GetAvailableUnitsAsync(int? propertyId, int? unitTypeId, int? bedrooms, decimal? maxRent);

    // UnitId -> the applicant's own open (Draft/Submitted/Returned) application for that unit.
    Task<Dictionary<int, int>> GetOpenApplicationUnitMapAsync(string applicantId);

    Task<Unit?> GetUnitAsync(int unitId);

    // Reference data for the Browse filter dropdowns.
    Task<List<UnitType>> GetUnitTypesAsync();
    Task<List<int>> GetDistinctBedroomCountsAsync();

    // Reuses an existing open application for the unit if the applicant already has one;
    // otherwise creates a new Draft. Fails if the unit currently has an active lease.
    Task<ServiceResult<RentalApplication>> StartApplicationAsync(Unit unit, string applicantId, string? applicantEmail);
}
