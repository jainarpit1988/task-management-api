using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface IExcelUploadErrorRepository
{
    Task AddRangeAsync(IEnumerable<ExcelUploadError> errors, CancellationToken ct);
}

