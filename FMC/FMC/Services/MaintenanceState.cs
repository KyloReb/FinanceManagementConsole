namespace FMC.Services;

public static class MaintenanceState
{
    private static bool _isActive;
    private static string _message = "System is undergoing scheduled maintenance.";
    private static string _modeType = "full";
    private static int _graceMinutes;
    private static DateTime? _scheduledAt;
    private static string? _scheduledMessage;
    private static readonly object _lock = new();

    public static bool IsActive
    {
        get { lock (_lock) { return _isActive; } }
    }

    public static string Message
    {
        get { lock (_lock) { return _message; } }
    }

    public static string ModeType
    {
        get { lock (_lock) { return _modeType; } }
    }

    public static int GraceMinutes
    {
        get { lock (_lock) { return _graceMinutes; } }
    }

    public static DateTime? ScheduledAt
    {
        get { lock (_lock) { return _scheduledAt; } }
    }

    public static void Sync(bool isActive, string? message, string modeType = "full", int graceMinutes = 0, DateTime? scheduledAt = null, string? scheduledMessage = null)
    {
        lock (_lock)
        {
            _isActive = isActive;
            _message = message ?? "System is undergoing scheduled maintenance.";
            _modeType = modeType;
            _graceMinutes = graceMinutes;
            _scheduledAt = scheduledAt;
            _scheduledMessage = scheduledMessage;
        }
    }

    public static void ClearSchedule()
    {
        lock (_lock)
        {
            _scheduledAt = null;
            _scheduledMessage = null;
        }
    }
}
