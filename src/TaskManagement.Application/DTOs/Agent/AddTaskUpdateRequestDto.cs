using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Agent;

public sealed class AddTaskUpdateRequestDto
{
    public TaskUpdateStatus Status { get; set; }
    public string? Comment { get; set; }
    public string? MeetingPersonName { get; set; }
    public string? MeetingPersonMobile { get; set; }
    public DateOnly? FollowupDate { get; set; }
}

