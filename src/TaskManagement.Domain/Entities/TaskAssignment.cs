using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public class TaskAssignment : AuditableEntity, ISoftDeletable
{
    public long Id { get; set; }

    public bool IsDeleted { get; set; }

    public long TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public long AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public long? AssignedBy { get; set; }
    public User? AssignedByUser { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

