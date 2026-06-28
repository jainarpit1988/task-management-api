using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(long id, bool includeDetails, CancellationToken ct);
    Task<(IReadOnlyList<TaskItem> Items, long TotalCount)> ListAsync(
        long? agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<IReadOnlyList<TaskItem>> ListAllAsync(
        long? agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        string? search,
        CancellationToken ct);

    Task<(IReadOnlyList<TaskItem> Items, long TotalCount)> ListForAgentAsync(
        long agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        bool? acknowledged,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<IReadOnlyList<TaskItem>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct);
    Task<bool> TrySelfAssignAsync(long taskId, long agentId, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<TaskItem> tasks, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

