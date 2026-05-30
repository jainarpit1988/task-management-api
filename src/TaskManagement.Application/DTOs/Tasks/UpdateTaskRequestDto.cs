using System.Text.Json.Serialization;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class UpdateTaskRequestDto
{
    public TaskStatus? Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? PdDate { get; set; }

    // FK to status_lookup (tasks.status) — PD Done, Pending, etc.
    public long? PdStatusId { get; set; }

    // FK to query_status_lookup (tasks.task_status).
    public long? TaskStatusId { get; set; }

    // Required when taskStatusId is "Other" (query_status_lookup).
    [JsonPropertyName("other_text")]
    public string? OtherText { get; set; }
}

