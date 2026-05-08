namespace TaskManagement.Application.DTOs.Agent;

public sealed class AcknowledgeTaskRequestDto
{
    public DateTime? AcknowledgedAtUtc { get; set; } // optional, defaults to now
}

