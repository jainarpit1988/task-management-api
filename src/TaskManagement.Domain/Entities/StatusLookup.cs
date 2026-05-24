namespace TaskManagement.Domain.Entities;

public sealed class StatusLookup
{
    public long StatusLookupId { get; set; }
    public string LookupName { get; set; } = null!;
    public string? LookupDescription { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
