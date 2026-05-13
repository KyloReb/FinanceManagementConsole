using System;

namespace FMC.Services;

/// <summary>
/// Service responsible for broadcasting high-priority system alerts across the entire application.
/// Allows the SystemHealthPanel to notify all active admin sessions when infrastructure is unstable.
/// </summary>
public class GlobalAlertService
{
    public class SystemAlert
    {
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "info"; // info, warning, error
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; }
    }

    private SystemAlert _currentAlert = new();

    public SystemAlert CurrentAlert => _currentAlert;

    public event Action? OnAlertChanged;

    /// <summary>
    /// Updates the global alert state. If the severity is "error", it triggers an app-wide broadcast.
    /// </summary>
    public void SetAlert(string message, string severity, bool isActive = true)
    {
        if (_currentAlert.Message != message || _currentAlert.Severity != severity || _currentAlert.IsActive != isActive)
        {
            _currentAlert = new SystemAlert
            {
                Message = message,
                Severity = severity,
                IsActive = isActive,
                Timestamp = DateTime.UtcNow
            };
            OnAlertChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears any active global alerts.
    /// </summary>
    public void ClearAlert()
    {
        if (_currentAlert.IsActive)
        {
            _currentAlert.IsActive = false;
            OnAlertChanged?.Invoke();
        }
    }
}
