namespace FMC.Shared.Time;

public static class DateTimeDisplayExtensions
{
    /// <summary>Philippines local time for UI and exports (stored values are UTC).</summary>
    public static DateTime ToDisplayTime(this DateTime value) => FmcDateTime.ToPhilippines(value);

    public static string ToDisplayString(this DateTime value, string format = "yyyy-MM-dd HH:mm:ss.fff") =>
        FmcDateTime.FormatPhilippines(value, format);
}
