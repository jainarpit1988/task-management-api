using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Reports;

public sealed class StatusSummaryReportDto
{
    public Dictionary<TaskStatus, long> Counts { get; set; } = new();
    public long Total => Counts.Values.Sum();
}

