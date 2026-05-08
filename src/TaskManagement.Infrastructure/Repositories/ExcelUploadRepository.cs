using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class ExcelUploadRepository : IExcelUploadRepository
{
    private readonly AppDbContext _db;
    public ExcelUploadRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ExcelUpload upload, CancellationToken ct)
    {
        await _db.ExcelUploads.AddAsync(upload, ct);
    }

    public Task<ExcelUpload?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.ExcelUploads
            .Include(x => x.Errors)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

