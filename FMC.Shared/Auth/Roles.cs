namespace FMC.Shared.Auth;

/// <summary>
/// Constants defining the primary authorization roles in the FMC system.
/// </summary>
public static class Roles
{
    /// <summary>
    /// System Administrator and Developer role. Complete access to user management and configuration.
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// Highest business privilege level. Access to all financial records, audit logs, and system governance.
    /// </summary>
    public const string CEO = "CEO";

    /// <summary>
    /// Financial operations role. Authorized to initiate credit/debit requests for cardholders.
    /// </summary>
    public const string Maker = "Maker";

    /// <summary>
    /// Financial verification role. Authorized to approve or reject pending transaction requests.
    /// </summary>
    public const string Approver = "Approver";

    /// <summary>
    /// Standard privilege level. Access to personal financial data only.
    /// </summary>
    public const string User = "User";
}
