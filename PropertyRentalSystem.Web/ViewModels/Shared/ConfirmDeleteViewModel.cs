namespace PropertyRentalSystem.Web.ViewModels.Shared;

public class ConfirmDeleteViewModel
{
    public string Title { get; set; } = "Confirm delete";
    public string Message { get; set; } = "Are you sure you want to delete this?";
    public string ActionUrl { get; set; } = string.Empty;
    public string RefreshUrl { get; set; } = string.Empty;
    public string RefreshTarget { get; set; } = "#properties-list";
}
