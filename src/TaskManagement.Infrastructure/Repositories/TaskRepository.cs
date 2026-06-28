using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;

    public TaskRepository(AppDbContext db) => _db = db;

    public Task<TaskItem?> GetByIdAsync(long id, bool includeDetails, CancellationToken ct)
    {
        var q = _db.Tasks.AsQueryable();
        if (includeDetails)
        {
            q = q.Include(x => x.Updates)
                .Include(x => x.Assignments)
                .Include(x => x.Acknowledgements);
        }

        return q.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<(IReadOnlyList<TaskItem> Items, long TotalCount)> ListAsync(
        long? agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var q = ApplyListFilters(_db.Tasks.AsNoTracking(), agentId, fromDate, toDate, status, search);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<TaskItem>> ListAllAsync(
        long? agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        string? search,
        CancellationToken ct)
    {
        var q = ApplyListFilters(_db.Tasks.AsNoTracking(), agentId, fromDate, toDate, status, search);
        return await q.OrderByDescending(x => x.Id).ToListAsync(ct);
    }

    private static IQueryable<TaskItem> ApplyListFilters(
        IQueryable<TaskItem> q,
        long? agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        string? search)
    {
        if (agentId.HasValue) q = q.Where(x => x.AssignedAgentId == agentId.Value);
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.ToDateTime(TimeOnly.MaxValue);
            q = q.Where(x => x.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(x =>
                x.InternalId.Contains(search) ||
                (x.ApplicationNo != null && x.ApplicationNo.Contains(search)) ||
                (x.CustomerName != null && x.CustomerName.Contains(search)) ||
                (x.MobileNo != null && x.MobileNo.Contains(search)) ||
                (x.EntityName != null && x.EntityName.Contains(search)));
        }

        return q;
    }

    public async Task<(IReadOnlyList<TaskItem> Items, long TotalCount)> ListForAgentAsync(
        long agentId,
        DateOnly? fromDate,
        DateOnly? toDate,
        TaskStatus? status,
        bool? acknowledged,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var q = _db.Tasks.AsNoTracking();

        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        if (acknowledged.HasValue) q = q.Where(x => x.Acknowledged == acknowledged.Value);
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.ToDateTime(TimeOnly.MaxValue);
            q = q.Where(x => x.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(x =>
                x.InternalId.Contains(search) ||
                (x.ApplicationNo != null && x.ApplicationNo.Contains(search)) ||
                (x.CustomerName != null && x.CustomerName.Contains(search)) ||
                (x.MobileNo != null && x.MobileNo.Contains(search)) ||
                (x.EntityName != null && x.EntityName.Contains(search)));
        }

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        var items = await _db.Tasks.Where(x => idList.Contains(x.Id)).ToListAsync(ct);
        return items;
    }

    public async Task<bool> TrySelfAssignAsync(long taskId, long agentId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Database.ExecuteSqlRawAsync(
            "UPDATE `tasks` SET `current_agent_id` = @p0, `updated_at` = @p1 WHERE `id` = @p2 AND `current_agent_id` IS NULL AND `is_deleted` = 0",
            [agentId, now, taskId],
            ct);

        if (rows == 0)
            return false;

        await _db.TaskAssignments.AddAsync(new TaskAssignment
        {
            TaskId = taskId,
            AgentId = agentId,
            AssignedBy = agentId,
            AssignedAt = now
        }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task AddRangeAsync(IEnumerable<TaskItem> tasks, CancellationToken ct)
    {
        await _db.Tasks.AddRangeAsync(tasks, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

