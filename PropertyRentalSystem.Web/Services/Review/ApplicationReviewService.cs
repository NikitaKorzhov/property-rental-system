using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.BusinessRules;
using PropertyRentalSystem.Web.Models;
using PropertyRentalSystem.Web.ViewModels.Review;

namespace PropertyRentalSystem.Web.Services.Review;

public class ApplicationReviewService : IApplicationReviewService
{
    private readonly ApplicationDbContext _db;

    public ApplicationReviewService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<PmApplicationListItemViewModel>> GetFilteredAsync(ApplicationStatus? status, int? propertyId)
    {
        var query = _db.RentalApplications.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (propertyId.HasValue)
        {
            query = query.Where(a => a.Unit.PropertyId == propertyId.Value);
        }

        return await query
            .OrderByDescending(a => a.Id)
            .Select(a => new PmApplicationListItemViewModel
            {
                Id = a.Id,
                ApplicantEmail = a.Applicant.Email!,
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                Status = a.Status
            })
            .ToListAsync();
    }

    public async Task<ApplicationDetailsViewModel?> GetDetailsAsync(int id) =>
        await _db.RentalApplications
            .Where(a => a.Id == id)
            .Select(a => new ApplicationDetailsViewModel
            {
                Id = a.Id,
                Status = a.Status,
                CanReview = a.Status == ApplicationStatus.Submitted
            })
            .FirstOrDefaultAsync();

    public async Task<List<StatusHistoryItemViewModel>> GetHistoryAsync(int applicationId) =>
        await _db.ApplicationStatusHistories
            .Where(h => h.RentalApplicationId == applicationId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new StatusHistoryItemViewModel
            {
                Status = h.Status,
                ChangedByEmail = h.ChangedBy.Email!,
                ChangedAt = h.ChangedAt,
                Comment = h.Comment
            })
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
        {
            return ServiceResult.Fail("This application can no longer be reviewed.");
        }

        switch (outcome)
        {
            case ReviewOutcome.Approve:
            {
                var today = DateTime.UtcNow.Date;
                var hasActiveLease = await _db.Leases
                    .Where(l => l.UnitId == application.UnitId)
                    .AnyAsync(LeaseRules.IsActiveOn(today));
                if (hasActiveLease)
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
                break;
            }

            case ReviewOutcome.Return:
            {
                application.Status = ApplicationStatus.Returned;
                break;
            }

            case ReviewOutcome.Deny:
            {
                application.Status = ApplicationStatus.Denied;
                break;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
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
