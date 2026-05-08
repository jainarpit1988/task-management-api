using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.Repositories;

public sealed class TaskAcknowledgementRepository : ITaskAcknowledgementRepository
{
    private readonly AppDbContext _db;
    public TaskAcknowledgementRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(TaskAcknowledgement ack, CancellationToken ct)
    {
        await _db.TaskAcknowledgements.AddAsync(ack, ct);
    }

    public async Task<IReadOnlyList<TaskAcknowledgement>> ListByTaskIdAsync(long taskId, CancellationToken ct)
    {
        return await _db.TaskAcknowledgements.AsNoTracking()
            .Where(x => x.TaskId == taskId)
            .OrderByDescending(x => x.AcknowledgedAt)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(long taskId, long agentId, CancellationToken ct) =>
        _db.TaskAcknowledgements.AnyAsync(x => x.TaskId == taskId && x.AgentId == agentId, ct);
}

