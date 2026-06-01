using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class TaskUpdate : AuditableEntity, ISoftDeletable
{
    public long Id { get; set; }

    public bool IsDeleted { get; set; }

    public long TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public long AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public TaskUpdateStatus Status { get; set; }

    // Maps to task_followups.comments
    public string? Comment { get; set; }

    public DateOnly? VisitDate { get; set; }
    public DateOnly? FollowupDate { get; set; }
    public DateOnly? NextFollowupDate { get; set; }

    public string? MeetingPersonName { get; set; }
    public string? MeetingPersonMobile { get; set; }

    public string? FollowupNotes { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? AttachmentUrl { get; set; }

    // NOTE: Other task_followups fields exist in DB; we add more as needed.
}

