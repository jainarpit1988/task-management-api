using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface IReportExportRepository
{
    Task AddAsync(ReportExport export, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

