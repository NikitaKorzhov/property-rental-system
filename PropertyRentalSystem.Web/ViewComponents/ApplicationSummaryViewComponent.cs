using Microsoft.AspNetCore.Mvc;
using PropertyRentalSystem.Web.Services.Applications;

namespace PropertyRentalSystem.Web.ViewComponents;

// Read-only "both sections" view used by the wizard's Summary step and the property
// manager's review details page.
public class ApplicationSummaryViewComponent : ViewComponent
{
    private readonly IApplicationSummaryService _summaries;

    public ApplicationSummaryViewComponent(IApplicationSummaryService summaries)
    {
        _summaries = summaries;
    }

    public async Task<IViewComponentResult> InvokeAsync(int applicationId)
    {
        var model = await _summaries.GetSummaryAsync(applicationId);
        return model == null ? Content(string.Empty) : View(model);
    }
}
