using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs;

public sealed class UserDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
}

