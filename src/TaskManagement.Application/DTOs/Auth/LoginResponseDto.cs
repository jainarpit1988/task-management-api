namespace TaskManagement.Application.DTOs.Auth;

public sealed class LoginResponseDto
{
    public string Token { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }

    public UserDto User { get; set; } = null!;
}

