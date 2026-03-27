namespace FMC.Shared.DTOs.Admin;

public class DocumentationDto
{
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Content { get; set; }
}
