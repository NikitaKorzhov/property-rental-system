using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Properties;
using PropertyRentalSystem.Web.ViewModels.Properties;
using PropertyRentalSystem.Web.ViewModels.Shared;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class PropertiesController : ModalFormControllerBase
{
    private readonly IPropertyService _properties;

    public PropertiesController(IPropertyService properties)
    {
        _properties = properties;
    }

    public async Task<IActionResult> Index()
    {
        return View(await LoadListAsync());
    }

    // Re-rendered into #properties-list by modal-forms.js after a create/edit succeeds.
    public async Task<IActionResult> List()
    {
        return PartialView("_PropertiesList", await LoadListAsync());
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

        await _properties.CreateAsync(model.Name, model.Address);
        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var property = await _properties.GetByIdAsync(id);
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

        var property = await _properties.GetByIdAsync(id);
        if (property == null) return NotFound();

        await _properties.UpdateAsync(property, model.Name, model.Address);
        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> DeleteConfirm(int id)
    {
        var property = await _properties.GetByIdAsync(id);
        if (property == null) return NotFound();

        return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", BuildDeleteConfirmModel(property));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var property = await _properties.GetByIdAsync(id);
        if (property == null) return NotFound();

        var result = await _properties.DeleteAsync(property);
        if (!result.Succeeded)
        {
            // Re-render the same confirm partial with the error, same as a failed Create/Edit.
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);
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

    private async Task<List<PropertyListItemViewModel>> LoadListAsync()
    {
        var properties = await _properties.GetAllAsync();
        return properties
            .Select(p => new PropertyListItemViewModel { Id = p.Id, Name = p.Name, Address = p.Address })
            .ToList();
    }
}
