using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Review;

public class ApplicationReviewService : IApplicationReviewService
{
    private readonly ApplicationDbContext _db;

    public ApplicationReviewService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<RentalApplication>> GetFilteredAsync(ApplicationStatus? status, int? propertyId)
    {
        var query = _db.RentalApplications
            .Include(a => a.Applicant)
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        if (propertyId.HasValue)
            query = query.Where(a => a.Unit.PropertyId == propertyId.Value);

        return await query.OrderByDescending(a => a.Id).ToListAsync();
    }

    public async Task<RentalApplication?> GetByIdAsync(int id) => await _db.RentalApplications.FindAsync(id);

    public async Task<List<ApplicationStatusHistory>> GetHistoryAsync(int applicationId) =>
        await _db.ApplicationStatusHistories
            .Include(h => h.ChangedBy)
            .Where(h => h.RentalApplicationId == applicationId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

    public async Task<RentalApplication?> GetReviewableAsync(int id)
    {
        var application = await _db.RentalApplications.FindAsync(id);
        return application != null && application.Status == ApplicationStatus.Submitted ? application : null;
    }

    public async Task<ServiceResult> ReviewAsync(RentalApplication application, ReviewOutcome outcome, string? comment, string reviewerUserId)
    {
        // Controllers reject posts that are not allowed: only a Submitted application can be reviewed.
        if (application.Status != ApplicationStatus.Submitted)
            return ServiceResult.Fail("This application can no longer be reviewed.");

        if (outcome == ReviewOutcome.Approve)
        {
            var today = DateTime.UtcNow.Date;
            var unitLeases = await _db.Leases.Where(l => l.UnitId == application.UnitId).ToListAsync();
            if (unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today)))
            {
                // The approval check prevents a second lease. Other open applications for
                // this unit are left as they are — only this one is rejected.
                return ServiceResult.Fail("This unit already has an active lease. Approval is blocked to prevent a second lease.");
            }

            application.Status = ApplicationStatus.Approved;
            application.Lease = new Lease
            {
                UnitId = application.UnitId,
                StartDate = today,
                EndDate = LeaseRules.ComputeEndDate(today)
            };
        }
        else if (outcome == ReviewOutcome.Return)
        {
            application.Status = ApplicationStatus.Returned;
        }
        else
        {
            application.Status = ApplicationStatus.Denied;
        }

        _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            Status = application.Status,
            ChangedByUserId = reviewerUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });

        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
