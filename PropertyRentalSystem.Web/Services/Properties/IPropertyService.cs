using PropertyRentalSystem.Web.Models;
using PropertyRentalSystem.Web.ViewModels.Properties;

namespace PropertyRentalSystem.Web.Services.Properties;

public interface IPropertyService
{
    // Projected straight to the ViewModel (Rule 9) — serves both the full property list
    // (which needs Address) and the Id+Name dropdowns (which just ignore it).
    Task<List<PropertyListItemViewModel>> GetAllAsync();
    Task<Property?> GetByIdAsync(int id);
    Task<Property> CreateAsync(string name, string address);
    Task UpdateAsync(Property property, string name, string address);
    Task<ServiceResult> DeleteAsync(Property property);
}
