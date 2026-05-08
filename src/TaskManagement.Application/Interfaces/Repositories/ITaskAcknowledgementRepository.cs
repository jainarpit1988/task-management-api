using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces.Repositories;

public interface ITaskAcknowledgementRepository
{
    Task AddAsync(TaskAcknowledgement ack, CancellationToken ct);
    Task<IReadOnlyList<TaskAcknowledgement>> ListByTaskIdAsync(long taskId, CancellationToken ct);
    Task<bool> ExistsAsync(long taskId, long agentId, CancellationToken ct);
}

