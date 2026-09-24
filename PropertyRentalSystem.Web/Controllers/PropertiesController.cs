using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.ViewModels.Properties;
using PropertyRentalSystem.Web.ViewModels.Shared;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class PropertiesController : Controller
{
    private readonly ApplicationDbContext _db;

    public PropertiesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        return View(await LoadPropertiesAsync());
    }

    // Re-rendered into #properties-list by modal-forms.js after a create/edit succeeds.
    public async Task<IActionResult> List()
    {
        return PartialView("_PropertiesList", await LoadPropertiesAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return PartialView("_PropertyForm", new PropertyFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PropertyFormViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_PropertyForm", model);

        _db.Properties.Add(new Property { Name = model.Name, Address = model.Address });
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property == null) return NotFound();

        return PartialView("_PropertyForm", new PropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name,
            Address = property.Address
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PropertyFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
            return PartialView("_PropertyForm", model);

        var property = await _db.Properties.FindAsync(id);
        if (property == null) return NotFound();

        property.Name = model.Name;
        property.Address = model.Address;
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> DeleteConfirm(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property == null) return NotFound();

        return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", BuildDeleteConfirmModel(property));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property == null) return NotFound();

        _db.Properties.Remove(property);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unit.PropertyId is a Restrict FK — a property with units can't cascade-delete them.
            // Re-render the same confirm partial with the error, same as a failed Create/Edit.
            ModelState.AddModelError(string.Empty, "Can't delete a property that still has units. Remove its units first.");
            return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", BuildDeleteConfirmModel(property));
        }

        return FormSuccess();
    }

    private ConfirmDeleteViewModel BuildDeleteConfirmModel(Property property) => new()
    {
        Title = "Delete Property",
        Message = $"Delete \"{property.Name}\"? This can't be undone.",
        ActionUrl = Url.Action(nameof(Delete), new { id = property.Id })!,
        RefreshUrl = Url.Action(nameof(List))!
    };

    private async Task<List<PropertyListItemViewModel>> LoadPropertiesAsync()
    {
        return await _db.Properties
            .OrderBy(p => p.Name)
            .Select(p => new PropertyListItemViewModel { Id = p.Id, Name = p.Name, Address = p.Address })
            .ToListAsync();
    }

    private IActionResult FormSuccess()
    {
        Response.Headers["X-Form-Success"] = "true";
        return NoContent();
    }
}
