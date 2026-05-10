using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/admin/tasks")]
[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.AGENT)}")]
public sealed class TasksController : ControllerBase
{
    private readonly IAdminService _admin;

    public TasksController(IAdminService admin) => _admin = admin;

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaskDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskDetailsDto>>> GetTaskDetails([FromRoute] long id, CancellationToken ct)
    {
        var result = await _admin.GetTaskDetailsForCallerAsync(id, ct);
        return Ok(ApiResponse<TaskDetailsDto>.Ok(result));
    }

    [HttpGet("{id:long}/history")]
    [ProducesResponseType(typeof(ApiResponse<TaskDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskDetailsDto>>> GetTaskHistory([FromRoute] long id, CancellationToken ct)
    {
        // For the current schema, "history" = task details including updates/assignments/acks.
        var result = await _admin.GetTaskDetailsForCallerAsync(id, ct);
        return Ok(ApiResponse<TaskDetailsDto>.Ok(result));
    }
}

