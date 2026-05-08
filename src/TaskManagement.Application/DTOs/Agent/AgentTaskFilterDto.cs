using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Agent;

public sealed class AgentTaskFilterDto
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public TaskStatus? Status { get; set; }
    public bool? Acknowledged { get; set; }
    public string? Search { get; set; }
}

