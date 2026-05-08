namespace TaskManagement.Application.DTOs.Tasks;

public sealed class TaskAssignmentDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long AgentId { get; set; }
    public long? AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

