namespace TaskManagement.Application.DTOs.Admin;

public sealed class AssignTasksRequestDto
{
    public long AgentId { get; set; }
    public List<long> TaskIds { get; set; } = new();
}

