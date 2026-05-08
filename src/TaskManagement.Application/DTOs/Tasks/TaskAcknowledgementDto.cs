namespace TaskManagement.Application.DTOs.Tasks;

public sealed class TaskAcknowledgementDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long AgentId { get; set; }
    public DateTime AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

