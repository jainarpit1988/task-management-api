using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using FluentValidation;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/admin/tasks")]
[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.AGENT)}")]
public sealed class TaskUpdateController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IValidator<UpdateTaskRequestDto> _validator;
    private readonly JsonSerializerOptions _json;

    public TaskUpdateController(
        IAdminService admin,
        IValidator<UpdateTaskRequestDto> validator,
        IOptions<JsonOptions> jsonOptions)
    {
        _admin = admin;
        _validator = validator;
        _json = jsonOptions.Value.JsonSerializerOptions;
    }

    [HttpPut("{id:long}/update")]
    [ProducesResponseType(typeof(ApiResponse<TaskDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskDetailsDto>>> UpdateTask(
        [FromRoute] long id,
        [FromBody] JsonElement body,
        CancellationToken ct)
    {
        // Accept both payload shapes:
        // 1) { "status": "...", "dueDate": "...", "pdDate": "...", "pdStatusId": 1, "taskStatusId": 13, "other_text": "..." }
        // 2) { "request": { ... } }
        UpdateTaskRequestDto request;
        try
        {
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("request", out var inner))
                request = inner.Deserialize<UpdateTaskRequestDto>(_json) ?? new UpdateTaskRequestDto();
            else
                request = body.Deserialize<UpdateTaskRequestDto>(_json) ?? new UpdateTaskRequestDto();
        }
        catch (JsonException)
        {
            return BadRequest(ApiResponse<object>.Fail("Invalid JSON payload."));
        }

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            // Keep response simple and consistent with the rest of the API wrapper.
            var msg = validation.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed";
            return BadRequest(ApiResponse<object>.Fail(msg));
        }

        var updated = await _admin.UpdateTaskAsync(id, request, ct);
        return Ok(ApiResponse<TaskDetailsDto>.Ok(updated, "Task updated"));
    }
}

