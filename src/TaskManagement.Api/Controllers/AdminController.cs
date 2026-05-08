using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Admin;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Reports;
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.ADMIN))]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService admin, ILogger<AdminController> logger)
    {
        _admin = admin;
        _logger = logger;
    }

    [HttpPost("agents")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateAgent([FromBody] CreateAgentRequestDto request, CancellationToken ct)
    {
        var agent = await _admin.CreateAgentAsync(request, ct);
        return Ok(ApiResponse<UserDto>.Ok(agent, "Agent created"));
    }

    [HttpGet("agents")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ListAgents(
        [FromQuery] string? search,
        [FromQuery] UserStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _admin.ListAgentsAsync(
            new AgentListFilterDto { Search = search, Status = status },
            new PaginationQueryDto { Page = page, PageSize = pageSize },
            ct);

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("upload-excel")]
    [RequestSizeLimit(100L * 1024 * 1024)] // 100 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 100L * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UploadExcel([FromForm] IFormFile? file, CancellationToken ct)
    {
        var traceId = HttpContext.TraceIdentifier;
        var contentLength = Request.ContentLength;
        Request.Headers.TryGetValue("Content-Type", out StringValues contentTypeHeader);

        _logger.LogInformation(
            "UploadExcel request started. traceId={TraceId} contentLength={ContentLength} contentType={ContentType}",
            traceId,
            contentLength,
            contentTypeHeader.ToString());

        if (file is null)
        {
            _logger.LogWarning("UploadExcel missing file. traceId={TraceId}", traceId);
            return BadRequest(ApiResponse<object>.Fail("Missing file. Send multipart/form-data with field name 'file'."));
        }

        if (file.Length <= 0)
        {
            _logger.LogWarning(
                "UploadExcel empty file. traceId={TraceId} fileName={FileName}",
                traceId,
                file.FileName);
            return BadRequest(ApiResponse<object>.Fail("Empty file"));
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "UploadExcel unsupported extension. traceId={TraceId} fileName={FileName} ext={Ext}",
                traceId,
                file.FileName,
                ext);
            return BadRequest(ApiResponse<object>.Fail("Only .xlsx files are supported"));
        }

        _logger.LogInformation(
            "UploadExcel received file. traceId={TraceId} fileName={FileName} sizeBytes={SizeBytes} contentType={FileContentType}",
            traceId,
            file.FileName,
            file.Length,
            file.ContentType);

        var startedAt = DateTime.UtcNow;
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var bytes = ms.ToArray();
        _logger.LogInformation(
            "UploadExcel file buffered. traceId={TraceId} bufferedBytes={BufferedBytes} elapsedMs={ElapsedMs}",
            traceId,
            bytes.Length,
            (DateTime.UtcNow - startedAt).TotalMilliseconds);

        var uploadId = await _admin.EnqueueExcelUploadAsync(bytes, file.FileName, ct);

        _logger.LogInformation(
            "UploadExcel queued successfully. traceId={TraceId} uploadId={UploadId} totalElapsedMs={ElapsedMs}",
            traceId,
            uploadId,
            (DateTime.UtcNow - startedAt).TotalMilliseconds);

        return Accepted(ApiResponse<object>.Ok(new { uploadId }, "Upload received. Processing in background; please wait for tasks to be generated."));
    }

    [HttpGet("upload-excel/history")]
    [ProducesResponseType(typeof(ApiResponse<ExcelUploadHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ExcelUploadHistoryDto>>> UploadExcelHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _admin.GetExcelUploadHistoryAsync(page, pageSize, ct);
        return Ok(ApiResponse<ExcelUploadHistoryDto>.Ok(result));
    }

    [HttpGet("upload-excel/{uploadId:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExcelUploadDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ExcelUploadDetailsDto>>> UploadExcelDetails(
        [FromRoute] long uploadId,
        [FromQuery] int recentErrors = 50,
        CancellationToken ct = default)
    {
        var result = await _admin.GetExcelUploadDetailsAsync(uploadId, recentErrors, ct);
        return Ok(ApiResponse<ExcelUploadDetailsDto>.Ok(result));
    }

    [HttpPost("tasks/assign")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AssignTasks([FromBody] AssignTasksRequestDto request, CancellationToken ct)
    {
        await _admin.AssignTasksAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(null, "Tasks assigned"));
    }

    [HttpPut("tasks/reassign")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ReassignTasks([FromBody] ReassignTasksRequestDto request, CancellationToken ct)
    {
        await _admin.ReassignTasksAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(null, "Tasks reassigned"));
    }

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetTasks(
        [FromQuery] long? agent_id,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] TaskStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _admin.GetTasksAsync(
            new TaskFilterDto { AgentId = agent_id, FromDate = fromDate, ToDate = toDate, Status = status, Search = search },
            new PaginationQueryDto { Page = page, PageSize = pageSize },
            ct);

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("tasks/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<TaskDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskDetailsDto>>> GetTaskDetails([FromRoute] long id, CancellationToken ct)
    {
        var result = await _admin.GetTaskDetailsAsync(id, ct);
        return Ok(ApiResponse<TaskDetailsDto>.Ok(result));
    }

    [HttpGet("tasks/{id:long}/history")]
    [ProducesResponseType(typeof(ApiResponse<TaskDetailsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TaskDetailsDto>>> GetTaskHistory([FromRoute] long id, CancellationToken ct)
    {
        // For the current schema, "history" = task details including followups/assignments/acks.
        var result = await _admin.GetTaskDetailsAsync(id, ct);
        return Ok(ApiResponse<TaskDetailsDto>.Ok(result));
    }

    [HttpGet("reports")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetReports(
        [FromQuery] long? agent_id,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] TaskStatus? status,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var filter = new TaskFilterDto { AgentId = agent_id, FromDate = fromDate, ToDate = toDate, Status = status, Search = search };
        var (tasksPerAgent, statusSummary) = await _admin.GetReportsAsync(filter, ct);
        return Ok(ApiResponse<object>.Ok(new { tasksPerAgent, statusSummary }));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] long? agent_id,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] TaskStatus? status,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var filter = new TaskFilterDto { AgentId = agent_id, FromDate = fromDate, ToDate = toDate, Status = status, Search = search };
        var (bytes, fileName) = await _admin.ExportReportsToExcelAsync(filter, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

