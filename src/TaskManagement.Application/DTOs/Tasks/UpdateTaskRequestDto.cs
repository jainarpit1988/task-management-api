using System.Text.Json.Serialization;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class UpdateTaskRequestDto
{
    // Update Status form -> tasks.current_status
    public TaskStatus? Status { get; set; }

    // PD status -> tasks.status (status_lookup FK)
    [JsonPropertyName("pdStatus")]
    public long? PdStatus { get; set; }

    // Query/task status -> tasks.task_status (query_status_lookup FK)
    [JsonPropertyName("taskStatus")]
    public long? TaskStatusLookupId { get; set; }

    // Required when taskStatus is "Other"
    [JsonPropertyName("other_text")]
    public string? OtherText { get; set; }

    [JsonIgnore]
    public bool OtherTextProvided { get; set; }

    public DateTime? PdDate { get; set; }
    public DateOnly? DueDate { get; set; }
}
