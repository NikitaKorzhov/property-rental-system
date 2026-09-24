using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PropertyRentalSystem.Web.Data;
using PropertyRentalSystem.Web.Domain.Rules;
using PropertyRentalSystem.Web.Models.Domain;

namespace PropertyRentalSystem.Web.Services.Applications;

public class ApplicationWizardService : IApplicationWizardService
{
    private readonly ApplicationDbContext _db;

    public ApplicationWizardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<RentalApplication>> GetMyApplicationsAsync(string applicantId, ApplicationStatus? status, int? propertyId)
    {
        var query = _db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Where(a => a.ApplicantId == applicantId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        if (propertyId.HasValue)
            query = query.Where(a => a.Unit.PropertyId == propertyId.Value);

        return await query.OrderByDescending(a => a.Id).ToListAsync();
    }

    public async Task<RentalApplication?> GetOwnedAsync(int applicationId, string applicantId)
    {
        var application = await _db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ResidenceHistories)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        return application != null && application.ApplicantId == applicantId ? application : null;
    }

    public WizardStep DetermineStep(RentalApplication application)
    {
        if (!RentalApplicationRules.IsEditable(application.Status))
            return WizardStep.Summary;

        if (!application.IsApplicantInfoComplete) return WizardStep.ApplicantInfo;
        if (!application.IsResidenceHistoryComplete) return WizardStep.ResidenceHistory;
        return WizardStep.ApplicantInfo;
    }

    public WizardStep GoBack(WizardStep currentStep) => currentStep switch
    {
        WizardStep.ResidenceHistory => WizardStep.ApplicantInfo,
        WizardStep.Summary => WizardStep.ResidenceHistory,
        _ => currentStep
    };

    public async Task<string?> GetReviewCommentAsync(RentalApplication application)
    {
        if (application.Status is not (ApplicationStatus.Returned or ApplicationStatus.Denied))
            return null;

        return await _db.ApplicationStatusHistories
            .Where(h => h.RentalApplicationId == application.Id && h.Status == application.Status)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => h.Comment)
            .FirstOrDefaultAsync();
    }

    public async Task<ServiceResult> SaveApplicantInfoAsync(
        RentalApplication application, string fullName, string phone, string email, string currentAddress)
    {
        if (!RentalApplicationRules.IsEditable(application.Status))
            return ServiceResult.Fail("This application can no longer be edited.");

        var errors = new List<ServiceError>();
        if (string.IsNullOrWhiteSpace(fullName))
            errors.Add(new ServiceError("FullName", "Full name is required."));
        if (string.IsNullOrWhiteSpace(phone))
            errors.Add(new ServiceError("Phone", "Phone is required."));
        if (string.IsNullOrWhiteSpace(email))
            errors.Add(new ServiceError("Email", "Email is required."));
        else if (!new EmailAddressAttribute().IsValid(email))
            errors.Add(new ServiceError("Email", "Enter a valid email address."));
        if (string.IsNullOrWhiteSpace(currentAddress))
            errors.Add(new ServiceError("CurrentAddress", "Current address is required."));

        if (errors.Count > 0)
            return ServiceResult.Fail(errors);

        application.FullName = fullName.Trim();
        application.Phone = phone.Trim();
        application.Email = email.Trim();
        application.CurrentAddress = currentAddress.Trim();
        application.IsApplicantInfoComplete = true;
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CompleteResidenceHistoryAsync(RentalApplication application)
    {
        if (!RentalApplicationRules.IsEditable(application.Status))
            return ServiceResult.Fail("This application can no longer be edited.");

        application.IsResidenceHistoryComplete = true;
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SubmitAsync(RentalApplication application, string userId)
    {
        if (!RentalApplicationRules.IsEditable(application.Status))
            return ServiceResult.Fail("This application can no longer be edited.");

        if (!RentalApplicationRules.CanSubmit(application.IsApplicantInfoComplete, application.IsResidenceHistoryComplete))
            return ServiceResult.Fail("Complete both sections before submitting.");

        var today = DateTime.UtcNow.Date;
        var unitLeases = await _db.Leases.Where(l => l.UnitId == application.UnitId).ToListAsync();
        if (unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today)))
            return ServiceResult.Fail("This unit currently has an active lease and can't accept new applications.");

        application.Status = ApplicationStatus.Submitted;
        _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            Status = ApplicationStatus.Submitted,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> WithdrawAsync(RentalApplication application, string userId)
    {
        if (!RentalApplicationRules.IsOpen(application.Status))
            return ServiceResult.Fail("This application can no longer be withdrawn.");

        application.Status = ApplicationStatus.Withdrawn;
        _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            Status = ApplicationStatus.Withdrawn,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
