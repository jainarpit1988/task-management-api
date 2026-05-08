using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Agent;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = nameof(UserRole.AGENT))]
public sealed class AgentController : ControllerBase
{
    private readonly IAgentService _agent;

    public AgentController(IAgentService agent) => _agent = agent;

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetMyTasks(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] TaskStatus? status,
        [FromQuery] bool? acknowledged,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _agent.GetMyTasksAsync(
            new AgentTaskFilterDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                Status = status,
                Acknowledged = acknowledged,
                Search = search
            },
            new PaginationQueryDto { Page = page, PageSize = pageSize },
            ct);

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("tasks/{id:long}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AcknowledgeTask([FromRoute] long id, [FromBody] AcknowledgeTaskRequestDto request, CancellationToken ct)
    {
        await _agent.AcknowledgeTaskAsync(id, request, ct);
        return Ok(ApiResponse<object>.Ok(null, "Task acknowledged"));
    }

    [HttpPost("tasks/{id:long}/update")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AddUpdate([FromRoute] long id, [FromBody] AddTaskUpdateRequestDto request, CancellationToken ct)
    {
        var update = await _agent.AddUpdateAsync(id, request, ct);
        return Ok(ApiResponse<object>.Ok(update, "Update added"));
    }

    [HttpGet("tasks/{id:long}/history")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> History([FromRoute] long id, CancellationToken ct)
    {
        var result = await _agent.GetTaskHistoryAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }
}

