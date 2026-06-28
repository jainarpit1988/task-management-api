using AutoMapper;
using TaskManagement.Application.Common;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Admin;
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
    private readonly IUserRepository _users;
    private readonly ITaskRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITaskUpdateRepository _updates;
    private readonly ITaskAcknowledgementRepository _acks;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public AgentService(
        IUserRepository users,
        ITaskRepository tasks,
        ITaskAssignmentRepository assignments,
        ITaskUpdateRepository updates,
        ITaskAcknowledgementRepository acks,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _users = users;
        _tasks = tasks;
        _assignments = assignments;
        _updates = updates;
        _acks = acks;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserDto>> ListAgentsAsync(AgentListFilterDto filter, PaginationQueryDto page, CancellationToken ct)
    {
        var (items, total) = await _users.ListAgentsAsync(filter.Search, filter.Status, page.Page, page.PageSize, ct);
        return new PagedResult<UserDto>
        {
            Items = items.Select(_mapper.Map<UserDto>).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total
        };
    }

    public async Task<PagedResult<TaskListItemDto>> GetMyTasksAsync(AgentTaskFilterDto filter, PaginationQueryDto page, CancellationToken ct)
    {
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

    public async Task ReassignTasksAsync(AgentReassignTasksRequestDto request, CancellationToken ct)
    {
        var fromId = _currentUser.UserId;
        if (request.ToAgentId == fromId)
            throw new AppException("Cannot reassign tasks to yourself");

        var to = await _users.GetByIdAsync(request.ToAgentId, ct);
        if (to is null || to.Role != UserRole.AGENT)
            throw new NotFoundException("Agent not found");

        var tasks = await _tasks.GetByIdsAsync(request.TaskIds, ct);
        if (tasks.Count != request.TaskIds.Distinct().Count())
            throw new AppException("One or more tasks not found");

        var now = DateTime.UtcNow;
        foreach (var t in tasks)
        {
            if (t.AssignedAgentId != fromId)
                throw new AppException($"Task {t.Id} is not assigned to you");
            t.AssignedAgentId = to.Id;
            t.Acknowledged = false;
            t.AcknowledgedAt = null;
            t.UpdatedAt = now;
        }

        var assignmentRows = tasks.Select(t => new TaskAssignment
        {
            TaskId = t.Id,
            AgentId = to.Id,
            AssignedBy = fromId,
            AssignedAt = now
        }).ToList();

        await _assignments.AddRangeAsync(assignmentRows, ct);
        await _tasks.SaveChangesAsync(ct);
    }

    public async Task AcknowledgeTaskAsync(long taskId, AcknowledgeTaskRequestDto request, CancellationToken ct)
    {
        await EnsureAgentCanModifyTaskAsync(taskId, ct);

        var task = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                   ?? throw new NotFoundException("Task not found");

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
        await EnsureAgentCanModifyTaskAsync(taskId, ct);

        var task = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                   ?? throw new NotFoundException("Task not found");

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
        await _tasks.TrySelfAssignAsync(taskId, _currentUser.UserId, ct);

        var task = await _tasks.GetByIdAsync(taskId, includeDetails: true, ct)
                   ?? throw new NotFoundException("Task not found");

        return _mapper.Map<TaskDetailsDto>(task);
    }

    private async Task EnsureAgentCanModifyTaskAsync(long taskId, CancellationToken ct)
    {
        var current = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                      ?? throw new NotFoundException("Task not found");

        if (!current.AssignedAgentId.HasValue)
            await _tasks.TrySelfAssignAsync(taskId, _currentUser.UserId, ct);

        current = await _tasks.GetByIdAsync(taskId, includeDetails: false, ct)
                  ?? throw new NotFoundException("Task not found");

        if (current.AssignedAgentId != _currentUser.UserId)
            throw new ForbiddenException("This task is assigned to another agent.");
    }
}

