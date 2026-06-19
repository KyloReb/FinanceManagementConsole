namespace FMC.Services;

public static class MaintenanceState
{
    private static bool _isActive;
    private static string _message = "System is undergoing scheduled maintenance.";
    private static readonly object _lock = new();
    private static DateTime? _scheduledAt;
    private static string? _scheduledMessage;
    private static DateTime _lastCheck = DateTime.MinValue;

    public static bool IsActive
    {
        get { lock (_lock) { return _isActive; } }
        set { lock (_lock) { _isActive = value; } }
    }

    public static string Message
    {
        get { lock (_lock) { return _message; } }
        set { lock (_lock) { _message = value ?? "System is undergoing scheduled maintenance."; } }
    }

    public static void Sync(bool isActive, string? message)
    {
        lock (_lock)
        {
            _isActive = isActive;
            _message = message ?? "System is undergoing scheduled maintenance.";
        }
    }

    public static void Sync(bool isActive, string? message, DateTime? scheduledAt, string? scheduledMessage)
    {
        lock (_lock)
        {
            _isActive = isActive;
            _message = message ?? "System is undergoing scheduled maintenance.";
            _scheduledAt = scheduledAt;
            _scheduledMessage = scheduledMessage;
        }
    }

    public static void CheckAutoActivate()
    {
        lock (_lock)
        {
            if (!_scheduledAt.HasValue || _isActive) return;
            if (DateTime.UtcNow < _scheduledAt.Value) return;
            if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 30) return;
            _lastCheck = DateTime.UtcNow;

            _isActive = true;
            _message = _scheduledMessage ?? "System is undergoing scheduled maintenance.";
            _scheduledAt = null;
            _scheduledMessage = null;
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
