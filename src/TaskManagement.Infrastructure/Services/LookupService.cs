using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Lookups;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Services;

public sealed class LookupService : ILookupService
{
    private readonly AppDbContext _db;

    public LookupService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<StatusLookupItemDto>> GetActiveStatusLookupsAsync(CancellationToken ct) =>
        await _db.StatusLookups.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.LookupName)
            .Select(x => new StatusLookupItemDto
            {
                Id = x.StatusLookupId,
                Name = x.LookupName,
                Description = x.LookupDescription
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QueryStatusLookupItemDto>> GetActiveQueryStatusLookupsAsync(CancellationToken ct) =>
        await _db.QueryStatusLookups.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.QueryStatusLookupName)
            .Select(x => new QueryStatusLookupItemDto
            {
                Id = x.QueryStatusLookupId,
                Name = x.QueryStatusLookupName,
                Description = x.QueryStatusLookupDescription
            })
            .ToListAsync(ct);
}
