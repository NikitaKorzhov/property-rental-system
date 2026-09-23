using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        await context.Database.MigrateAsync();

        string[] roles = { "PropertyManager", "Applicant" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var managerEmail = "manager@radency.com";
        if (await userManager.FindByEmailAsync(managerEmail) == null)
        {
            var manager = new ApplicationUser { UserName = managerEmail, Email = managerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(manager, "Password123!");
            await userManager.AddToRoleAsync(manager, "PropertyManager");
        }

        var applicantEmail = "applicant@radency.com";
        var applicantUser = await userManager.FindByEmailAsync(applicantEmail);
        if (applicantUser == null)
        {
            applicantUser = new ApplicationUser { UserName = applicantEmail, Email = applicantEmail, EmailConfirmed = true };
            await userManager.CreateAsync(applicantUser, "Password123!");
            await userManager.AddToRoleAsync(applicantUser, "Applicant");
        }

        if (!context.UnitTypes.Any())
        {
            var unitTypes = new List<UnitType>
            {
                new UnitType { Name = "Studio", IsActive = true },
                new UnitType { Name = "1-Bedroom", IsActive = true },
                new UnitType { Name = "2-Bedroom", IsActive = false } // inactive per requirements
            };
            context.UnitTypes.AddRange(unitTypes);
            await context.SaveChangesAsync();
        }

        if (!context.Properties.Any())
        {
            var propertyFaker = new Faker<Property>()
                .RuleFor(p => p.Name, f => f.Company.CompanyName())
                .RuleFor(p => p.Address, f => f.Address.StreetAddress());

            var properties = propertyFaker.Generate(3);
            context.Properties.AddRange(properties);
            await context.SaveChangesAsync();

            var unitTypeIds = context.UnitTypes.Where(ut => ut.IsActive).Select(ut => ut.Id).ToList();

            foreach (var prop in properties)
            {
                var unitFaker = new Faker<Unit>()
                    .RuleFor(u => u.UnitNumber, f => f.Random.AlphaNumeric(3).ToUpper())
                    .RuleFor(u => u.Bedrooms, f => f.Random.Number(1, 3))
                    .RuleFor(u => u.MonthlyRent, f => f.Random.Decimal(400, 1500))
                    .RuleFor(u => u.PropertyId, _ => prop.Id)
                    .RuleFor(u => u.UnitTypeId, f => f.PickRandom(unitTypeIds));

                var units = unitFaker.Generate(3);
                context.Units.AddRange(units);
            }
            await context.SaveChangesAsync();
        }

        if (!context.RentalApplications.Any())
        {
            var firstUnit = await context.Units.FirstAsync();
            
            var application = new RentalApplication
            {
                ApplicantId = applicantUser.Id,
                UnitId = firstUnit.Id,
                Status = ApplicationStatus.Submitted,
                FullName = "Nikita Korzhov",
                Phone = "+380665044427",
                Email = applicantEmail,
                CurrentAddress = "Boryspil, Kyiv Oblast"
            };

            context.RentalApplications.Add(application);
            await context.SaveChangesAsync();
        }
    }
}