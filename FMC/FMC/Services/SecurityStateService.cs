using System;

namespace FMC.Services;

/// <summary>
/// Scoped service to track security-related UI states (like cool-down timers) within a user's session.
/// </summary>
public class SecurityStateService
{
    private DateTime? _lockoutEndDate;
    private int _failedAttempts;

    public int FailedAttempts => _failedAttempts;
    
    public bool IsLockedOut => _lockoutEndDate.HasValue && DateTime.UtcNow < _lockoutEndDate.Value;
    
    public int RemainingSeconds => _lockoutEndDate.HasValue 
        ? (int)Math.Max(0, (_lockoutEndDate.Value - DateTime.UtcNow).TotalSeconds) 
        : 0;

    public void RecordFailure()
    {
        _failedAttempts++;
        if (_failedAttempts >= 3)
        {
            _lockoutEndDate = DateTime.UtcNow.AddSeconds(30);
        }
    }

    public void Reset()
    {
        _failedAttempts = 0;
        _lockoutEndDate = null;
    }
}
