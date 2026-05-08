using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class ReportExportRepository : IReportExportRepository
{
    private readonly AppDbContext _db;
    public ReportExportRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ReportExport export, CancellationToken ct)
    {
        await _db.ReportExports.AddAsync(export, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

