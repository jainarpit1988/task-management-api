using System.Text.Json.Serialization;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class TaskListItemDto
{
    public long Id { get; set; }
    public string InternalId { get; set; } = null!;
    public string? ApplicationNo { get; set; }
    public string? CustomerName { get; set; }
    public string? MobileNo { get; set; }

    public string? PhoneNo => MobileNo;
    public string? Phone => MobileNo;

    public long? AssignedAgentId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PdDate { get; set; }

    public TaskStatus Status { get; set; }

    [JsonPropertyName("pdStatusId")]
    public long? PdStatus { get; set; }

    [JsonPropertyName("taskStatusId")]
    public long? TaskStatusLookupId { get; set; }

    [JsonPropertyName("other_text")]
    public string? OtherText { get; set; }

    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
