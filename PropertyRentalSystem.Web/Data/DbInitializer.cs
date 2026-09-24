using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Data;

public static class DbInitializer
{
    private static readonly Dictionary<string, int> BedroomsByUnitTypeName = new()
    {
        ["Studio"] = 0,
        ["1-Bedroom"] = 1,
        ["2-Bedroom"] = 2
    };

    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Deterministic seed data so demos/recordings are reproducible across clean-DB restarts.
        Randomizer.Seed = new Random(42);

        await context.Database.MigrateAsync();

        string[] roles = { Roles.PropertyManager, Roles.Applicant };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var manager = await EnsureUserAsync(userManager, "manager@radency.com", Roles.PropertyManager);
        var applicant = await EnsureUserAsync(userManager, "applicant@radency.com", Roles.Applicant);

        var managers = new List<ApplicationUser>
        {
            manager,
            await EnsureUserAsync(userManager, "manager2@radency.com", Roles.PropertyManager)
        };

        var applicants = new List<ApplicationUser> { applicant };
        for (var i = 2; i <= 5; i++)
        {
            applicants.Add(await EnsureUserAsync(userManager, $"applicant{i}@radency.com", Roles.Applicant));
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

            var allUnitTypes = await context.UnitTypes.ToListAsync();
            var activeUnitTypeIds = allUnitTypes.Where(t => t.IsActive).Select(t => t.Id).ToList();
            var inactiveUnitTypeIds = allUnitTypes.Where(t => !t.IsActive).Select(t => t.Id).ToList();
            var bedroomsByTypeId = allUnitTypes.ToDictionary(t => t.Id, t => BedroomsByUnitTypeName.GetValueOrDefault(t.Name, 1));

            var rentFaker = new Faker();
            for (var propertyIndex = 0; propertyIndex < properties.Count; propertyIndex++)
            {
                for (var unitIndex = 0; unitIndex < 3; unitIndex++)
                {
                    // Assign the inactive "2-Bedroom" type to one unit each on the first two
                    // properties, so the "inactive type still shows on units that already use
                    // it, but can't be picked for others" rule has real data to demonstrate.
                    var unitTypeId = propertyIndex < 2 && unitIndex == 0 && inactiveUnitTypeIds.Count > 0
                        ? inactiveUnitTypeIds[0]
                        : rentFaker.PickRandom(activeUnitTypeIds);

                    context.Units.Add(new Unit
                    {
                        UnitNumber = (101 + unitIndex).ToString(),
                        Bedrooms = bedroomsByTypeId[unitTypeId],
                        MonthlyRent = Math.Round(rentFaker.Random.Decimal(400, 1500) / 50m) * 50m,
                        PropertyId = properties[propertyIndex].Id,
                        UnitTypeId = unitTypeId
                    });
                }
            }
            await context.SaveChangesAsync();
        }

        // Every application status must be represented, each tied to a distinct unit and
        // carrying a status-change history (who, when, comment) as required by the spec.
        if (!context.RentalApplications.Any())
        {
            var units = await context.Units.OrderBy(u => u.Id).ToListAsync();
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
                .RuleFor(r => r.MoveOutDate, (f, r) =>
                {
                    var candidate = r.MoveInDate.AddMonths(f.Random.Number(6, 24));
                    return candidate > DateTime.UtcNow ? DateTime.UtcNow : candidate;
                });

            var infoFaker = new Faker();
            var submittedAt = DateTime.UtcNow.AddDays(-10);
            Unit? approvedUnit = null;

            for (var i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i];
                var unit = units[i];
                var applicantUser = applicants[i % applicants.Count];
                var reviewer = managers[i % managers.Count];

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
                        application.Lease = new Lease
                        {
                            Unit = unit,
                            StartDate = DateTime.UtcNow.Date,
                            EndDate = LeaseRules.ComputeEndDate(DateTime.UtcNow.Date)
                        };
                        approvedUnit = unit;
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

            // A second Submitted application on the unit that already has an active lease, so the
            // "reject submit/approval when the unit has an active lease, leaving other open
            // applications for it as they are" rule has data to demonstrate.
            if (approvedUnit != null)
            {
                var conflictingApplicant = applicants[statuses.Length % applicants.Count];
                var conflictingApplication = new RentalApplication
                {
                    ApplicantId = conflictingApplicant.Id,
                    UnitId = approvedUnit.Id,
                    Status = ApplicationStatus.Submitted,
                    FullName = infoFaker.Name.FullName(),
                    Phone = infoFaker.Phone.PhoneNumber(),
                    Email = conflictingApplicant.Email!,
                    CurrentAddress = infoFaker.Address.FullAddress(),
                    IsApplicantInfoComplete = true,
                    IsResidenceHistoryComplete = true
                };
                conflictingApplication.ResidenceHistories.Add(residenceFaker.Generate());
                conflictingApplication.StatusHistory.Add(new ApplicationStatusHistory
                {
                    Status = ApplicationStatus.Draft,
                    ChangedBy = conflictingApplicant,
                    ChangedAt = submittedAt.AddDays(-1)
                });
                conflictingApplication.StatusHistory.Add(new ApplicationStatusHistory
                {
                    Status = ApplicationStatus.Submitted,
                    ChangedBy = conflictingApplicant,
                    ChangedAt = submittedAt.AddDays(1)
                });
                context.RentalApplications.Add(conflictingApplication);
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

            var createResult = await userManager.CreateAsync(user, "Password123!");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create seed user '{email}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to add seed user '{email}' to role '{role}': {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
        return user;
    }
}
