using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public class ExcelUploadError : AuditableEntity
{
    public long Id { get; set; }

    public long? UploadId { get; set; }
    public ExcelUpload? Upload { get; set; }

    public int? ExcelRowNumber { get; set; }
    public string ErrorMessage { get; set; } = null!;

    public string? RawData { get; set; } // stored as JSON string
}

