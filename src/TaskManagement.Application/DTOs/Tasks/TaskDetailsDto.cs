using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class TaskDetailsDto
{
    public long Id { get; set; }
    public string InternalId { get; set; } = null!;
    public string? ApplicationNo { get; set; }
    public string? CustomerName { get; set; }
    public string? MobileNo { get; set; }

    // Alias exposed to clients that bind to a "Phone No." field.
    // Always reflects MobileNo so the same value renders under either key.
    public string? PhoneNo => MobileNo;
    public string? Phone => MobileNo;

    public string? CustomerAddress { get; set; }

    public long? AssignedAgentId { get; set; }

    public TaskStatus Status { get; set; }
    public long? LastUpdateId { get; set; }

    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public string? RawData { get; set; }

    public List<TaskUpdateDto> Updates { get; set; } = new();
    public List<TaskAssignmentDto> Assignments { get; set; } = new();
    public List<TaskAcknowledgementDto> Acknowledgements { get; set; } = new();
}

