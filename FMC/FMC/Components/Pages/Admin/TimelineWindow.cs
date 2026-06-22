namespace FMC.Components.Pages.Admin;

public class TimelineWindow
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Label { get; set; } = "";
    public bool IsActive { get; set; }
    public string? ActivatedBy { get; set; }
    public string? DeactivatedBy { get; set; }
    public string? Details { get; set; }
}
