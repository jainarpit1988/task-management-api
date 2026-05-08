using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public sealed class AuditLog : AuditableEntity
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }

    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }

    public string? OldValue { get; set; } // JSON
    public string? NewValue { get; set; } // JSON

    public string? IpAddress { get; set; }
}

