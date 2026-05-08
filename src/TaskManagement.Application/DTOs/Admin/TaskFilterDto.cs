using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Admin;

public sealed class TaskFilterDto
{
    public long? AgentId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public TaskStatus? Status { get; set; }
    public string? Search { get; set; } // internalId/applicationNo/customer/mobile
}

