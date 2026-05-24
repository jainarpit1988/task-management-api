namespace TaskManagement.Application.DTOs.Lookups;

public sealed class QueryStatusLookupItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
