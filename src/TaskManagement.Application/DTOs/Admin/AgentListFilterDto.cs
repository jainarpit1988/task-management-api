using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Admin;

public sealed class AgentListFilterDto
{
    public string? Search { get; set; }
    public UserStatus? Status { get; set; }
}

