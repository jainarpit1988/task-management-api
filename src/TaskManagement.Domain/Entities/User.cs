using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class User : AuditableEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.ACTIVE;

    public bool IsDeleted { get; set; }

    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskUpdate> TaskUpdates { get; set; } = new List<TaskUpdate>();
}

