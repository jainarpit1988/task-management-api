using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using FluentValidation;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Agent;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.Helpers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/admin/tasks")]
[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.AGENT)}")]
public sealed class TaskUpdateController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IValidator<UpdateTaskRequestDto> _metadataValidator;
    private readonly IValidator<AddTaskUpdateRequestDto> _followUpValidator;

    public TaskUpdateController(
        IAdminService admin,
        IValidator<UpdateTaskRequestDto> metadataValidator,
        IValidator<AddTaskUpdateRequestDto> followUpValidator)
    {
        _admin = admin;
        _metadataValidator = metadataValidator;
        _followUpValidator = followUpValidator;
    }

    [HttpPut("{id:long}/update")]
    [ProducesResponseType(typeof(ApiResponse<TaskDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskDetailsDto>>> UpdateTask(
        [FromRoute] long id,
        [FromBody] JsonElement body,
        CancellationToken ct)
    {
        JsonElement root;
        try
        {
            root = UnwrapRequestBody(body);
        }
        catch (JsonException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }

        AddTaskUpdateRequestDto? followUp = null;
        UpdateTaskRequestDto? metadata = null;

        try
        {
            if (TryParseFollowUpRequest(root, out var parsedFollowUp))
            {
                followUp = parsedFollowUp;
                metadata = ParseMetadataRequest(root, includeWorkflowStatus: false);
            }
            else
            {
                metadata = ParseMetadataRequest(root, includeWorkflowStatus: true);
            }
        }
        catch (JsonException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }

        var hasFollowUp = followUp is not null;
        var hasMetadata = HasMetadataFields(metadata);

        if (!hasFollowUp && !hasMetadata)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Provide at least one field: status/current_status, pdStatus, taskStatusId, pdDate, dueDate, other_text, " +
                "or a follow-up (comment, meetingPersonName, meetingPersonMobile, followupDate)."));
        }

        if (hasFollowUp)
        {
            var followUpValidation = await _followUpValidator.ValidateAsync(followUp!, ct);
            if (!followUpValidation.IsValid)
            {
                var msg = followUpValidation.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed";
                return BadRequest(ApiResponse<object>.Fail(msg));
            }
        }

        if (hasMetadata)
        {
            var metadataValidation = await _metadataValidator.ValidateAsync(metadata!, ct);
            if (!metadataValidation.IsValid)
            {
                var msg = metadataValidation.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed";
                return BadRequest(ApiResponse<object>.Fail(msg));
            }
        }

        TaskDetailsDto updated;
        if (hasFollowUp && hasMetadata)
        {
            await _admin.AddTaskFollowUpAsync(id, followUp!, ct);
            updated = await _admin.UpdateTaskAsync(id, metadata!, ct);
        }
        else if (hasFollowUp)
        {
            updated = await _admin.AddTaskFollowUpAsync(id, followUp!, ct);
        }
        else
        {
            updated = await _admin.UpdateTaskAsync(id, metadata!, ct);
        }

        return Ok(ApiResponse<TaskDetailsDto>.Ok(updated, "Task updated"));
    }

    private static JsonElement UnwrapRequestBody(JsonElement body)
    {
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("request", out var inner))
            return inner;

        if (body.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected JSON object.");

        return body;
    }

    private static bool HasMetadataFields(UpdateTaskRequestDto request) =>
        request.Status.HasValue ||
        request.PdStatus.HasValue ||
        request.TaskStatusLookupId.HasValue ||
        request.PdDate.HasValue ||
        request.DueDate.HasValue ||
        request.OtherTextProvided;

    private static bool TryParseFollowUpRequest(JsonElement root, out AddTaskUpdateRequestDto request)
    {
        request = new AddTaskUpdateRequestDto();

        var hasFollowUpField =
            TryGetStringProperty(root, "comment", out _) ||
            TryGetStringProperty(root, "meetingPersonName", out _) ||
            TryGetStringProperty(root, "meeting_person_name", out _) ||
            TryGetStringProperty(root, "meetingPersonMobile", out _) ||
            TryGetStringProperty(root, "meeting_person_mobile", out _) ||
            root.TryGetProperty("followupDate", out _) ||
            root.TryGetProperty("follow_up_date", out _);

        if (!hasFollowUpField)
            return false;

        if (root.TryGetProperty("current_status", out var statusEl) ||
            root.TryGetProperty("status", out statusEl))
        {
            request.Status = ParseFollowUpStatus(statusEl);
        }

        if (TryGetStringProperty(root, "comment", out var comment))
            request.Comment = comment;

        if (TryGetStringProperty(root, "meetingPersonName", out var meetingName) ||
            TryGetStringProperty(root, "meeting_person_name", out meetingName))
        {
            request.MeetingPersonName = meetingName;
        }

        if (TryGetStringProperty(root, "meetingPersonMobile", out var meetingMobile) ||
            TryGetStringProperty(root, "meeting_person_mobile", out meetingMobile))
        {
            request.MeetingPersonMobile = meetingMobile;
        }

        if (root.TryGetProperty("followupDate", out var followupEl) ||
            root.TryGetProperty("follow_up_date", out followupEl))
        {
            request.FollowupDate = ParseDateOnly(followupEl, "followupDate");
        }

        return true;
    }

    private static UpdateTaskRequestDto ParseMetadataRequest(JsonElement root, bool includeWorkflowStatus)
    {
        var request = new UpdateTaskRequestDto();

        if (includeWorkflowStatus)
        {
            if (root.TryGetProperty("status", out var statusEl))
                request.Status = ParseWorkflowStatus(statusEl);
            else if (root.TryGetProperty("current_status", out statusEl))
                request.Status = ParseWorkflowStatus(statusEl);
        }

        if (TryGetInt64(root, "pdStatus", out var pdStatus))
            request.PdStatus = pdStatus;
        else if (TryGetInt64(root, "pdStatusId", out pdStatus))
            request.PdStatus = pdStatus;

        if (TryGetInt64(root, "taskStatus", out var taskStatus))
            request.TaskStatusLookupId = taskStatus;
        else if (TryGetInt64(root, "taskStatusId", out taskStatus))
            request.TaskStatusLookupId = taskStatus;

        if (root.TryGetProperty("other_text", out var otherTextEl))
            request.OtherTextProvided = ApplyOtherTextFromElement(request, otherTextEl);
        else if (root.TryGetProperty("otherText", out otherTextEl))
            request.OtherTextProvided = ApplyOtherTextFromElement(request, otherTextEl);

        if (root.TryGetProperty("pdDate", out var pdDateEl))
            request.PdDate = ParseDateTime(pdDateEl, "pdDate");

        if (root.TryGetProperty("dueDate", out var dueDateEl))
            request.DueDate = ParseDateOnly(dueDateEl, "dueDate");

        return request;
    }

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
            return false;

        value = el.GetString();
        return true;
    }

    private static bool ApplyOtherTextFromElement(UpdateTaskRequestDto request, JsonElement el)
    {
        request.OtherText = el.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => el.GetString(),
            _ => throw new JsonException("other_text must be a string.")
        };
        return true;
    }

    private static DateTime? ParseDateTime(JsonElement el, string fieldName)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
            {
                var text = el.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                try
                {
                    return IndiaDateTime.ParseToUtc(text);
                }
                catch (FormatException ex)
                {
                    throw new JsonException($"Invalid {fieldName} value: {text}", ex);
                }
            }
            default:
                throw new JsonException($"{fieldName} must be an ISO date/time string.");
        }
    }

    private static DateOnly? ParseDateOnly(JsonElement el, string fieldName)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
            {
                var text = el.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var dateOnly))
                    return dateOnly;
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    return DateOnly.FromDateTime(dt);
                if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
                    return DateOnly.FromDateTime(dt);
                throw new JsonException($"Invalid {fieldName} value: {text}");
            }
            default:
                throw new JsonException($"{fieldName} must be a date string.");
        }
    }

    private static TaskUpdateStatus ParseFollowUpStatus(JsonElement statusEl)
    {
        switch (statusEl.ValueKind)
        {
            case JsonValueKind.String:
            {
                var text = statusEl.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    throw new JsonException("current_status is required for follow-up updates.");
                if (Enum.TryParse<TaskUpdateStatus>(text, ignoreCase: true, out var parsed))
                    return parsed;
                throw new JsonException($"Invalid current_status value: {text}");
            }
            case JsonValueKind.Number when statusEl.TryGetInt32(out var numeric):
            {
                if (Enum.IsDefined(typeof(TaskUpdateStatus), numeric))
                    return (TaskUpdateStatus)numeric;
                throw new JsonException($"Invalid current_status value: {numeric}");
            }
            default:
                throw new JsonException("current_status must be a string or number.");
        }
    }

    private static TaskStatus? ParseWorkflowStatus(JsonElement statusEl)
    {
        switch (statusEl.ValueKind)
        {
            case JsonValueKind.String:
            {
                var text = statusEl.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                if (Enum.TryParse<TaskStatus>(text, ignoreCase: true, out var parsed))
                    return parsed;
                throw new JsonException($"Invalid status value: {text}");
            }
            case JsonValueKind.Number when statusEl.TryGetInt32(out var numeric):
            {
                if (Enum.IsDefined(typeof(TaskStatus), numeric))
                    return (TaskStatus)numeric;
                throw new JsonException($"Invalid status value: {numeric}");
            }
            default:
                throw new JsonException("status must be a workflow status string or number.");
        }
    }

    private static bool TryGetInt64(JsonElement root, string propertyName, out long value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Number)
            return false;

        value = el.GetInt64();
        return true;
    }
}
