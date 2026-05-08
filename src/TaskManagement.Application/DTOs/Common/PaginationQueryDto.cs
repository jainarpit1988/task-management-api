namespace TaskManagement.Application.DTOs.Common;

public sealed class PaginationQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

