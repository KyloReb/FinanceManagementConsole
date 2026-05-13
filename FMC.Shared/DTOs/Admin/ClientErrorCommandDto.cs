using System;

namespace FMC.Shared.DTOs.Admin;

public class ClientErrorCommandDto
{
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string? Component { get; set; }
    public DateTime Timestamp { get; set; }
}
