namespace TaskManagement.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
