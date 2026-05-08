namespace TaskManagement.Application.DTOs.Auth;

public sealed class LoginRequestDto
{
    public string EmailOrMobile { get; set; } = null!;
    public string Password { get; set; } = null!;
}

