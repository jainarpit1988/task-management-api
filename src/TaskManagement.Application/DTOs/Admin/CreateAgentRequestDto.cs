namespace TaskManagement.Application.DTOs.Admin;

public sealed class CreateAgentRequestDto
{
    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string Password { get; set; } = null!;
}

