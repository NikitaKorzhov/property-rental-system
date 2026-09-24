using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.Services.Units;
using PropertyRentalSystem.Web.ViewModels.Shared;
using PropertyRentalSystem.Web.ViewModels.Units;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class UnitsController : ModalFormControllerBase
{
    private readonly IUnitService _units;

    public UnitsController(IUnitService units)
    {
        _units = units;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int propertyId)
    {
        if (!await _units.PropertyExistsAsync(propertyId))
            return NotFound();

        var model = new UnitFormViewModel { PropertyId = propertyId };
        await PopulateUnitTypesAsync(model, currentUnitTypeId: null);
        return PartialView("_UnitForm", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitFormViewModel model)
    {
        var result = await _units.CreateAsync(model.PropertyId, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);
            await PopulateUnitTypesAsync(model, currentUnitTypeId: null);
            return PartialView("_UnitForm", model);
        }

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var unit = await _units.GetByIdAsync(id);
        if (unit == null) return NotFound();

        var model = new UnitFormViewModel
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            MonthlyRent = unit.MonthlyRent,
            UnitTypeId = unit.UnitTypeId
        };
        await PopulateUnitTypesAsync(model, currentUnitTypeId: unit.UnitTypeId);
        return PartialView("_UnitForm", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UnitFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var unit = await _units.GetByIdAsync(id);
        if (unit == null) return NotFound();

        var result = await _units.UpdateAsync(unit, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);
            await PopulateUnitTypesAsync(model, currentUnitTypeId: unit.UnitTypeId);
            return PartialView("_UnitForm", model);
        }

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> DeleteConfirm(int id)
    {
        var unit = await _units.GetByIdAsync(id);
        if (unit == null) return NotFound();

        return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", BuildDeleteConfirmModel(unit));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var unit = await _units.GetByIdAsync(id);
        if (unit == null) return NotFound();

        var result = await _units.DeleteAsync(unit);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);
            return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", BuildDeleteConfirmModel(unit));
        }

        return FormSuccess();
    }

    private ConfirmDeleteViewModel BuildDeleteConfirmModel(Unit unit) => new()
    {
        Title = "Delete Unit",
        Message = $"Delete unit \"{unit.UnitNumber}\"? This can't be undone.",
        ActionUrl = Url.Action(nameof(Delete), new { id = unit.Id })!,
        RefreshUrl = Url.Action(nameof(PropertiesController.List), "Properties")!
    };

    private async Task PopulateUnitTypesAsync(UnitFormViewModel model, int? currentUnitTypeId)
    {
        var types = await _units.GetSelectableUnitTypesAsync(currentUnitTypeId);
        model.UnitTypeOptions = types
            .Select(t => new SelectListItem(t.IsActive ? t.Name : $"{t.Name} (Inactive)", t.Id.ToString()))
            .ToList();
    }
}
