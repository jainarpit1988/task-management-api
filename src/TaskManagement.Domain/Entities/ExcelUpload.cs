using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class ExcelUpload : AuditableEntity
{
    public long Id { get; set; }

    public string? FileName { get; set; }
    public string? FilePath { get; set; }

    public long? UploadedBy { get; set; }
    public User? UploadedByUser { get; set; }

    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }

    public ExcelUploadStatus Status { get; set; } = ExcelUploadStatus.PROCESSING;

    public ICollection<ExcelUploadError> Errors { get; set; } = new List<ExcelUploadError>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
}

