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

        var manager = await EnsureUserAsync(userManager, "manager@radency.com", "PropertyManager");
        var applicant = await EnsureUserAsync(userManager, "applicant@radency.com", "Applicant");

        var managers = new List<ApplicationUser> { manager };
        for (var i = 2; i <= 2; i++)
        {
            managers.Add(await EnsureUserAsync(userManager, $"manager{i}@radency.com", "PropertyManager"));
        }

        var applicants = new List<ApplicationUser> { applicant };
        for (var i = 2; i <= 5; i++)
        {
            applicants.Add(await EnsureUserAsync(userManager, $"applicant{i}@radency.com", "Applicant"));
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

        // Every application status must be represented, each tied to a distinct unit and
        // carrying a status-change history (who, when, comment) as required by the spec.
        if (!context.RentalApplications.Any())
        {
            var units = await context.Units.OrderBy(u => u.Id).Take(6).ToListAsync();
            var statuses = new[]
            {
                ApplicationStatus.Draft,
                ApplicationStatus.Submitted,
                ApplicationStatus.Returned,
                ApplicationStatus.Approved,
                ApplicationStatus.Denied,
                ApplicationStatus.Withdrawn
            };

            var residenceFaker = new Faker<ResidenceHistory>()
                .RuleFor(r => r.Address, f => f.Address.FullAddress())
                .RuleFor(r => r.LandlordName, f => f.Name.FullName())
                .RuleFor(r => r.LandlordPhone, f => f.Phone.PhoneNumber())
                .RuleFor(r => r.MoveInDate, f => f.Date.Past(3, DateTime.UtcNow.AddYears(-1)))
                .RuleFor(r => r.MoveOutDate, (f, r) => r.MoveInDate.AddMonths(f.Random.Number(6, 24)));

            var infoFaker = new Faker();

            for (var i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i];
                var unit = units[i];
                var applicantUser = applicants[i % applicants.Count];
                var reviewer = managers[i % managers.Count];
                var submittedAt = DateTime.UtcNow.AddDays(-10);

                var application = new RentalApplication
                {
                    ApplicantId = applicantUser.Id,
                    UnitId = unit.Id,
                    Status = status,
                    FullName = infoFaker.Name.FullName(),
                    Phone = infoFaker.Phone.PhoneNumber(),
                    Email = applicantUser.Email!,
                    CurrentAddress = infoFaker.Address.FullAddress(),
                    IsApplicantInfoComplete = true,
                    IsResidenceHistoryComplete = status != ApplicationStatus.Draft
                };

                application.StatusHistory.Add(new ApplicationStatusHistory
                {
                    Status = ApplicationStatus.Draft,
                    ChangedBy = applicantUser,
                    ChangedAt = submittedAt.AddDays(-2)
                });

                if (status != ApplicationStatus.Draft)
                {
                    application.ResidenceHistories.Add(residenceFaker.Generate());
                    application.StatusHistory.Add(new ApplicationStatusHistory
                    {
                        Status = ApplicationStatus.Submitted,
                        ChangedBy = applicantUser,
                        ChangedAt = submittedAt
                    });
                }

                switch (status)
                {
                    case ApplicationStatus.Returned:
                        application.StatusHistory.Add(new ApplicationStatusHistory
                        {
                            Status = ApplicationStatus.Returned,
                            ChangedBy = reviewer,
                            ChangedAt = submittedAt.AddDays(2),
                            Comment = "Please provide complete residence history for the last 2 years."
                        });
                        break;
                    case ApplicationStatus.Approved:
                        application.StatusHistory.Add(new ApplicationStatusHistory
                        {
                            Status = ApplicationStatus.Approved,
                            ChangedBy = reviewer,
                            ChangedAt = submittedAt.AddDays(2),
                            Comment = "Application meets all criteria."
                        });
                        context.Leases.Add(new Lease
                        {
                            Unit = unit,
                            RentalApplication = application,
                            StartDate = DateTime.UtcNow.Date,
                            EndDate = DateTime.UtcNow.Date.AddMonths(12)
                        });
                        break;
                    case ApplicationStatus.Denied:
                        application.StatusHistory.Add(new ApplicationStatusHistory
                        {
                            Status = ApplicationStatus.Denied,
                            ChangedBy = reviewer,
                            ChangedAt = submittedAt.AddDays(2),
                            Comment = "Income does not meet the minimum requirement for this unit."
                        });
                        break;
                    case ApplicationStatus.Withdrawn:
                        application.StatusHistory.Add(new ApplicationStatusHistory
                        {
                            Status = ApplicationStatus.Withdrawn,
                            ChangedBy = applicantUser,
                            ChangedAt = submittedAt.AddDays(1)
                        });
                        break;
                }

                context.RentalApplications.Add(application);
            }

            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, "Password123!");
            await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }
}
