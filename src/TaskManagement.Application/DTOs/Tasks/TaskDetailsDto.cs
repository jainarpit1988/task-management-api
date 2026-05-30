using System.Text.Json.Serialization;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class TaskDetailsDto
{
    public long Id { get; set; }
    public string InternalId { get; set; } = null!;
    public string? ApplicationNo { get; set; }
    public string? CustomerName { get; set; }
    public string? MobileNo { get; set; }

    public string? PhoneNo => MobileNo;
    public string? Phone => MobileNo;

    public string? CustomerAddress { get; set; }

    public long? AssignedAgentId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PdDate { get; set; }

    // tasks.current_status (Update Status form)
    public TaskStatus Status { get; set; }

    // tasks.status (status_lookup FK)
    [JsonPropertyName("pdStatusId")]
    public long? PdStatus { get; set; }

    // tasks.task_status (query_status_lookup FK)
    [JsonPropertyName("taskStatusId")]
    public long? TaskStatusLookupId { get; set; }

    [JsonPropertyName("other_text")]
    public string? OtherText { get; set; }

    public long? LastUpdateId { get; set; }

    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public string? RawData { get; set; }

    public List<TaskUpdateDto> Updates { get; set; } = new();
    public List<TaskAssignmentDto> Assignments { get; set; } = new();
    public List<TaskAcknowledgementDto> Acknowledgements { get; set; } = new();
}
