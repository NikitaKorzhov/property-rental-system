using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Properties;

public class PropertyService : IPropertyService
{
    private readonly ApplicationDbContext _db;

    public PropertyService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Property>> GetAllAsync() =>
        await _db.Properties.OrderBy(p => p.Name).ToListAsync();

    public async Task<Property?> GetByIdAsync(int id) =>
        await _db.Properties.FindAsync(id);

    public async Task<Property> CreateAsync(string name, string address)
    {
        var property = new Property { Name = name, Address = address };
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        return property;
    }

    public async Task UpdateAsync(Property property, string name, string address)
    {
        property.Name = name;
        property.Address = address;
        await _db.SaveChangesAsync();
    }

    // A property with units can't be removed — checked proactively (rather than relying on
    // the Restrict FK to throw) so the rule is expressible and testable without a real
    // relational database enforcing constraints.
    public async Task<ServiceResult> DeleteAsync(Property property)
    {
        var hasUnits = await _db.Units.AnyAsync(u => u.PropertyId == property.Id);
        if (hasUnits)
            return ServiceResult.Fail("Can't delete a property that still has units. Remove its units first.");

        _db.Properties.Remove(property);
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
