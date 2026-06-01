namespace TaskManagement.Application.DTOs.Admin;

public sealed class ArchiveDataResultDto
{
    public DateTime ArchivedAtUtc { get; init; }
    public IReadOnlyDictionary<string, int> ArchivedCounts { get; init; } = new Dictionary<string, int>();
}
