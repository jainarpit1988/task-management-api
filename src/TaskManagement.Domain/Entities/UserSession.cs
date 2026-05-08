using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public sealed class UserSession : AuditableEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string Token { get; set; } = null!;
    public string? DeviceInfo { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

