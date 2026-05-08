using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskManagement.Infrastructure.Persistence;

public sealed class DateOnlyConverter : ValueConverter<DateOnly?, DateTime?>
{
    public DateOnlyConverter() : base(
        v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue) : null,
        v => v.HasValue ? DateOnly.FromDateTime(v.Value) : null)
    { }
}

public sealed class DateOnlyComparer : ValueComparer<DateOnly?>
{
    public DateOnlyComparer() : base(
        (l, r) => l.Equals(r),
        v => v.GetHashCode(),
        v => v)
    { }
}

