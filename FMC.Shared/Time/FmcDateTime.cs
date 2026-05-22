namespace FMC.Shared.Time;

/// <summary>
/// UTC storage with Philippines (PHT, UTC+8) display for audit and financial logs.
/// </summary>
public static class FmcDateTime
{
    private static readonly TimeZoneInfo PhilippinesZone = ResolvePhilippinesZone();

    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Database/API values are stored as UTC; unspecified kinds are treated as UTC.
    /// </summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime ToPhilippines(DateTime utcOrUnspecified) =>
        TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcOrUnspecified), PhilippinesZone);

    public static string FormatPhilippines(DateTime utcOrUnspecified, string format = "yyyy-MM-dd HH:mm:ss.fff") =>
        $"{ToPhilippines(utcOrUnspecified).ToString(format)} PHT";

    public static string FormatPhilippinesNow(string format = "yyyy-MM-dd HH:mm:ss.fff") =>
        FormatPhilippines(UtcNow, format);

    private static TimeZoneInfo ResolvePhilippinesZone()
    {
        foreach (var id in new[] { "Asia/Manila", "Singapore Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("PHT", TimeSpan.FromHours(8), "Philippines", "Philippines");
    }
}
