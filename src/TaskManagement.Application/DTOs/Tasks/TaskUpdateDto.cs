using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Tasks;

public sealed class TaskUpdateDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long AgentId { get; set; }
    public TaskUpdateStatus Status { get; set; }
    public string? Comment { get; set; }
    public string? MeetingPersonName { get; set; }
    public string? MeetingPersonMobile { get; set; }
    public DateOnly? FollowupDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

