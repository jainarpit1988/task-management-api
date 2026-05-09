namespace TaskManagement.Application.DTOs.Agent;

public sealed class AgentReassignTasksRequestDto
{
    public long ToAgentId { get; set; }

    public List<long> TaskIds { get; set; } = new();
}
