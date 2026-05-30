using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManagement.Application.Helpers;

namespace TaskManagement.Infrastructure.Persistence;

internal static class UtcDateTimeConverter
{
    public static readonly ValueConverter<DateTime, DateTime> NonNullable = new(
        v => IndiaDateTime.ToDbValue(v),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> Nullable = new(
        v => v.HasValue ? IndiaDateTime.ToDbValue(v.Value) : null,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);
}
