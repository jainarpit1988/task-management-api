using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Interfaces;

public interface ICurrentUser
{
    long UserId { get; }
    UserRole Role { get; }
    string? Email { get; }
    string? Mobile { get; }
}

