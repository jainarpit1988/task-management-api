using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.DTOs.Admin;

public sealed class ExcelUploadHistoryItemDto
{
    public long Id { get; set; }
    public string? FileName { get; set; }
    public ExcelUploadStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public sealed class ExcelUploadHistoryDto
{
    public IReadOnlyList<ExcelUploadHistoryItemDto> Items { get; set; } = Array.Empty<ExcelUploadHistoryItemDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
}

public sealed class ExcelUploadDetailsDto
{
    public long Id { get; set; }
    public string? FileName { get; set; }
    public ExcelUploadStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public IReadOnlyList<ExcelUploadErrorItemDto> RecentErrors { get; set; } = Array.Empty<ExcelUploadErrorItemDto>();
}

public sealed class ExcelUploadErrorItemDto
{
    public long Id { get; set; }
    public int? ExcelRowNumber { get; set; }
    public string ErrorMessage { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

