using System.ComponentModel.DataAnnotations;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.ViewModels.Account;

public class RegisterViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Display(Name = "I am a")] public string Role { get; set; } = Roles.Applicant;
}