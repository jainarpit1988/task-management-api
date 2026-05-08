using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public class TaskAcknowledgement : AuditableEntity
{
    public long Id { get; set; }

    public long TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public long AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
}

