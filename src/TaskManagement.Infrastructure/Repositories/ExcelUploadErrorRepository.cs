using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class ExcelUploadErrorRepository : IExcelUploadErrorRepository
{
    private readonly AppDbContext _db;
    public ExcelUploadErrorRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<ExcelUploadError> errors, CancellationToken ct)
    {
        await _db.ExcelUploadErrors.AddRangeAsync(errors, ct);
    }
}

