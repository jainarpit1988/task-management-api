using TaskManagement.Application.DTOs.Lookups;

namespace TaskManagement.Application.Interfaces;

public interface ILookupService
{
    Task<IReadOnlyList<StatusLookupItemDto>> GetActiveStatusLookupsAsync(CancellationToken ct);
    Task<IReadOnlyList<QueryStatusLookupItemDto>> GetActiveQueryStatusLookupsAsync(CancellationToken ct);
}
