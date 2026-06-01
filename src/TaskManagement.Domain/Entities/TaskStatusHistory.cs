using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public sealed class TaskStatusHistory : ISoftDeletable
{
    public long Id { get; set; }

    public long? TaskId { get; set; }
    public TaskItem? Task { get; set; }

    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    public long? ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
}
