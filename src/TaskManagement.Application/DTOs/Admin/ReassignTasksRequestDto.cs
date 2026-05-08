namespace TaskManagement.Application.DTOs.Admin;

public sealed class ReassignTasksRequestDto
{
    public long FromAgentId { get; set; }
    public long ToAgentId { get; set; }
    public List<long> TaskIds { get; set; } = new();
}

