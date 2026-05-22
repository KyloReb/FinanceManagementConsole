namespace FMC.Shared.Time;

/// <summary>
/// Consistent forensic detail text for wallet sync and mother-account adjustments.
/// </summary>
public static class FinancialAuditFormatter
{
    public static string WithRecordedAt(string body, DateTime? recordedAtUtc = null)
    {
        var at = recordedAtUtc ?? FmcDateTime.UtcNow;
        return $"Recorded: {FmcDateTime.FormatPhilippines(at)} | {body}";
    }

    public static string MotherAccountAdjustment(
        decimal formerWallet,
        decimal formerUsage,
        decimal formerRemaining,
        decimal formerMotherBalance,
        decimal adjustmentAmount,
        decimal newMotherBalance,
        decimal newWallet,
        decimal newUsage,
        decimal newRemaining,
        Guid transactionId,
        string? memo,
        string? adminEmail,
        string? accountNumber = null)
    {
        var direction = adjustmentAmount >= 0 ? "CREDIT" : "DEBIT";
        var body =
            $"{direction} mother account by {Math.Abs(adjustmentAmount):C} | " +
            $"Memo: {memo ?? "—"} | Admin: {adminEmail ?? "—"} | " +
            (accountNumber != null ? $"Acct: {accountNumber} | " : string.Empty) +
            $"Txn: {transactionId} | " +
            $"Before — Wallet: {formerWallet:C}, Usage: {formerUsage:C}, Remaining: {formerRemaining:C}, Mother: {formerMotherBalance:C} | " +
            $"After — Wallet: {newWallet:C}, Usage: {newUsage:C}, Remaining: {newRemaining:C}, Mother: {newMotherBalance:C}";
        return WithRecordedAt(body);
    }

    public static string SyncLimitReset(
        decimal formerWallet,
        decimal formerUsage,
        decimal formerRemaining,
        decimal newWallet,
        decimal newUsage,
        decimal newRemaining,
        string? adminEmail = null)
    {
        var body =
            $"Daily wallet reset | Admin: {adminEmail ?? "System"} | " +
            $"Before — Wallet: {formerWallet:C}, Usage: {formerUsage:C}, Remaining: {formerRemaining:C} | " +
            $"After — Wallet: {newWallet:C}, Usage: {newUsage:C}, Remaining: {newRemaining:C}";
        return WithRecordedAt(body);
    }
}
