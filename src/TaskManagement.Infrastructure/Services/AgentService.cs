using AutoMapper;
using TaskManagement.Application.Common;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.DTOs.Agent;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Services;

public sealed class AgentService : IAgentService
{
    private readonly ITaskRepository _tasks;
    private readonly ITaskUpdateRepository _updates;
    private readonly ITaskAcknowledgementRepository _acks;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public AgentService(
        ITaskRepository tasks,
        ITaskUpdateRepository updates,
        ITaskAcknowledgementRepository acks,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _tasks = tasks;
        _updates = updates;
        _acks = acks;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<PagedResult<TaskListItemDto>> GetMyTasksAsync(AgentTaskFilterDto filter, PaginationQueryDto page, CancellationToken ct)
    {
        if (!filter.FromDate.HasValue && !filter.ToDate.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            filter.FromDate = today;
            filter.ToDate = today;
        }

        var (items, total) = await _tasks.ListForAgentAsync(
            _currentUser.UserId,
            filter.FromDate,
            filter.ToDate,
            filter.Status,
            filter.Acknowledged,
            filter.Search,
            page.Page,
            page.PageSize,
            ct);

        return new PagedResult<TaskListItemDto>
        {
            Items = items.Select(_mapper.Map<TaskListItemDto>).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total
        };
    }

    public async Task AcknowledgeTaskAsync(long taskId, AcknowledgeTaskRequestDto request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                   ?? throw new NotFoundException("Task not found");

        EnsureOwnTask(task);

        if (await _acks.ExistsAsync(taskId, _currentUser.UserId, ct))
            throw new AppException("Task already acknowledged");

        var ackAt = request.AcknowledgedAtUtc ?? DateTime.UtcNow;

        task.Acknowledged = true;
        task.AcknowledgedAt = ackAt;
        task.UpdatedAt = DateTime.UtcNow;

        await _acks.AddAsync(new TaskAcknowledgement
        {
            TaskId = taskId,
            AgentId = _currentUser.UserId,
            AcknowledgedAt = ackAt
        }, ct);

        await _tasks.SaveChangesAsync(ct);
    }

    public async Task<TaskUpdateDto> AddUpdateAsync(long taskId, AddTaskUpdateRequestDto request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                   ?? throw new NotFoundException("Task not found");

        EnsureOwnTask(task);

        var update = new TaskUpdate
        {
            TaskId = taskId,
            AgentId = _currentUser.UserId,
            Status = request.Status,
            Comment = request.Comment,
            MeetingPersonName = request.MeetingPersonName,
            MeetingPersonMobile = request.MeetingPersonMobile,
            FollowupDate = request.FollowupDate
        };

        await _updates.AddAsync(update, ct);

        // Business rule: latest update defines task status
        task.LastUpdateId = null; // set after SaveChanges (id available); we update in second SaveChanges
        // Map followup status -> current task status (same names in enums)
        task.Status = Enum.TryParse<TaskStatus>(request.Status.ToString(), out var mapped)
            ? mapped
            : TaskStatus.PENDING;
        task.UpdatedAt = DateTime.UtcNow;

        await _tasks.SaveChangesAsync(ct);

        // now that update has an Id, set last_update_id and persist
        task.LastUpdateId = update.Id;
        await _tasks.SaveChangesAsync(ct);

        return _mapper.Map<TaskUpdateDto>(update);
    }

    public async Task<TaskDetailsDto> GetTaskHistoryAsync(long taskId, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, includeDetails: true, ct)
                   ?? throw new NotFoundException("Task not found");

        EnsureOwnTask(task);

        return _mapper.Map<TaskDetailsDto>(task);
    }

    private void EnsureOwnTask(TaskItem task)
    {
        if (task.AssignedAgentId != _currentUser.UserId)
            throw new ForbiddenException("You cannot access tasks assigned to other agents");
    }
}

