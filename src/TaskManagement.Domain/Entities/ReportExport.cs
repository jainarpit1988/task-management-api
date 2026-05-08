using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public class ReportExport : AuditableEntity
{
    public long Id { get; set; }

    public string? FileName { get; set; }
    public string? FilePath { get; set; }

    public long? GeneratedBy { get; set; }
    public User? GeneratedByUser { get; set; }
}

