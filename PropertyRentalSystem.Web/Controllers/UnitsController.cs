using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Models.Domain;
using PropertyRentalSystem.Web.ViewModels.Shared;
using PropertyRentalSystem.Web.ViewModels.Units;

namespace PropertyRentalSystem.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class UnitsController : Controller
{
    private readonly ApplicationDbContext _db;

    public UnitsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int propertyId)
    {
        if (!await _db.Properties.AnyAsync(p => p.Id == propertyId))
            return NotFound();

        var model = new UnitFormViewModel { PropertyId = propertyId };
        await PopulateUnitTypesAsync(model, currentUnitTypeId: null);
        return PartialView("_UnitForm", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitFormViewModel model)
    {
        await ValidateAsync(model, currentUnitTypeId: null, currentUnitId: null);
        if (!ModelState.IsValid)
        {
            await PopulateUnitTypesAsync(model, currentUnitTypeId: null);
            return PartialView("_UnitForm", model);
        }

        _db.Units.Add(new Unit
        {
            PropertyId = model.PropertyId,
            UnitNumber = model.UnitNumber,
            Bedrooms = model.Bedrooms,
            MonthlyRent = model.MonthlyRent,
            UnitTypeId = model.UnitTypeId
        });
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var unit = await _db.Units.FindAsync(id);
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

        var unit = await _db.Units.FindAsync(id);
        if (unit == null) return NotFound();

        // The property a unit belongs to isn't something this form is allowed to change —
        // trust the DB record, not whatever the client posted in the hidden field.
        model.PropertyId = unit.PropertyId;

        await ValidateAsync(model, currentUnitTypeId: unit.UnitTypeId, currentUnitId: id);
        if (!ModelState.IsValid)
        {
            await PopulateUnitTypesAsync(model, currentUnitTypeId: unit.UnitTypeId);
            return PartialView("_UnitForm", model);
        }

        unit.UnitNumber = model.UnitNumber;
        unit.Bedrooms = model.Bedrooms;
        unit.MonthlyRent = model.MonthlyRent;
        unit.UnitTypeId = model.UnitTypeId;
        await _db.SaveChangesAsync();

        return FormSuccess();
    }

    [HttpGet]
    public async Task<IActionResult> DeleteConfirm(int id)
    {
        var unit = await _db.Units.FindAsync(id);
        if (unit == null) return NotFound();

        return PartialView("~/Views/Shared/_ConfirmDelete.cshtml", BuildDeleteConfirmModel(unit));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var unit = await _db.Units.FindAsync(id);
        if (unit == null) return NotFound();

        _db.Units.Remove(unit);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // RentalApplication.UnitId and Lease.UnitId are Restrict FKs.
            // Re-render the same confirm partial with the error, same as a failed Create/Edit.
            ModelState.AddModelError(string.Empty, "Can't delete a unit that has applications or a lease against it.");
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

    // Enforced on the server: an inactive unit type may stay on the unit that already has
    // it, but can never be (re)selected — neither for a different unit, nor as a new choice
    // when editing this one.
    private async Task ValidateAsync(UnitFormViewModel model, int? currentUnitTypeId, int? currentUnitId)
    {
        if (!await _db.Properties.AnyAsync(p => p.Id == model.PropertyId))
        {
            ModelState.AddModelError(string.Empty, "Property not found.");
            return;
        }

        var unitTypeUnchanged = currentUnitTypeId.HasValue && model.UnitTypeId == currentUnitTypeId.Value;
        if (!unitTypeUnchanged)
        {
            var isActive = await _db.UnitTypes.AnyAsync(t => t.Id == model.UnitTypeId && t.IsActive);
            if (!isActive)
                ModelState.AddModelError(nameof(model.UnitTypeId), "This unit type is inactive and can't be assigned.");
        }

        var duplicateNumber = await _db.Units.AnyAsync(u =>
            u.PropertyId == model.PropertyId &&
            u.UnitNumber == model.UnitNumber &&
            (!currentUnitId.HasValue || u.Id != currentUnitId.Value));
        if (duplicateNumber)
            ModelState.AddModelError(nameof(model.UnitNumber), "This property already has a unit with this number.");
    }

    private async Task PopulateUnitTypesAsync(UnitFormViewModel model, int? currentUnitTypeId)
    {
        var types = await _db.UnitTypes
            .Where(t => t.IsActive || t.Id == currentUnitTypeId)
            .OrderBy(t => t.Name)
            .ToListAsync();

        model.UnitTypeOptions = types
            .Select(t => new SelectListItem(t.IsActive ? t.Name : $"{t.Name} (Inactive)", t.Id.ToString()))
            .ToList();
    }

    private IActionResult FormSuccess()
    {
        Response.Headers["X-Form-Success"] = "true";
        return NoContent();
    }
}
