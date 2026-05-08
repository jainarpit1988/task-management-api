using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface IExcelUploadRepository
{
    Task AddAsync(ExcelUpload upload, CancellationToken ct);
    Task<ExcelUpload?> GetByIdAsync(long id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

