using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Properties;

public interface IPropertyService
{
    Task<List<Property>> GetAllAsync();
    Task<Property?> GetByIdAsync(int id);
    Task<Property> CreateAsync(string name, string address);
    Task UpdateAsync(Property property, string name, string address);
    Task<ServiceResult> DeleteAsync(Property property);
}
