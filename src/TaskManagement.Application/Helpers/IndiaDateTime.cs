using System.Globalization;

namespace TaskManagement.Application.Helpers;

public static class IndiaDateTime
{
    public static TimeSpan IstOffset { get; } = TimeSpan.FromHours(5.5);

    private static readonly TimeZoneInfo IstZone = ResolveIstZone();

    private static TimeZoneInfo ResolveIstZone()
    {
        foreach (var id in new[] { "India Standard Time", "Asia/Kolkata" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { /* try next */ }
            catch (InvalidTimeZoneException) { /* try next */ }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "IST",
            IstOffset,
            "India Standard Time",
            "India Standard Time");
    }

    /// <summary>Normalize any incoming instant to UTC for DB storage.</summary>
    public static DateTime ToUtcForStorage(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static DateTime? ToUtcForStorage(DateTime? value) =>
        value.HasValue ? ToUtcForStorage(value.Value) : null;

    /// <summary>UTC instant as Unspecified kind for MySQL datetime columns (avoids driver local conversion).</summary>
    public static DateTime ToDbValue(DateTime value) =>
        DateTime.SpecifyKind(ToUtcForStorage(value), DateTimeKind.Unspecified);

    public static DateTime? ToDbValue(DateTime? value) =>
        value.HasValue ? ToDbValue(value.Value) : null;

    /// <summary>Parse API/Excel date text to UTC. Honors offsets; plain values are treated as IST.</summary>
    public static DateTime ParseToUtc(string text)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            return dto.UtcDateTime;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), IstZone);

        throw new FormatException($"Invalid date value: {text}");
    }

    public static DateTime IstDateOnlyToUtc(DateOnly date)
    {
        var istLocal = date.ToDateTime(TimeOnly.MinValue);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(istLocal, DateTimeKind.Unspecified), IstZone);
    }

    public static DateTime IstLocalToUtc(DateTime istLocal)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(istLocal, DateTimeKind.Unspecified), IstZone);
    }

    public static DateTime? IstLocalToUtc(DateTime? istLocal) =>
        istLocal.HasValue ? IstLocalToUtc(istLocal.Value) : null;

    /// <summary>Convert UTC value from DB to IST for API responses.</summary>
    public static DateTime FromUtcToIst(DateTime utc)
    {
        var normalized = ToUtcForStorage(utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalized, IstZone);
    }

    public static DateTime? FromUtcToIst(DateTime? utc) =>
        utc.HasValue ? FromUtcToIst(utc.Value) : null;
}
