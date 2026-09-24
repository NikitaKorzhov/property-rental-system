using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyRentalSystem.Web.ViewModels.Units;

public class UnitFormViewModel
{
    public int Id { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Unit Number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Required, Range(0, 10)]
    public int Bedrooms { get; set; }

    [Required, Range(0.01, 100000)]
    [Display(Name = "Monthly Rent")]
    public decimal MonthlyRent { get; set; }

    [Required, Display(Name = "Unit Type")]
    public int UnitTypeId { get; set; }

    public List<SelectListItem> UnitTypeOptions { get; set; } = new();
}
