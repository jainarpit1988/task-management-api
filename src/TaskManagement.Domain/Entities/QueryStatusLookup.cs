namespace TaskManagement.Domain.Entities;

public sealed class QueryStatusLookup
{
    public long QueryStatusLookupId { get; set; }
    public string QueryStatusLookupName { get; set; } = null!;
    public string? QueryStatusLookupDescription { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
