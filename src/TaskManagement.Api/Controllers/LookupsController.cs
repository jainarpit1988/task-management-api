using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Lookups;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/lookups")]
[Route("api/admin/lookups")]
[Route("api/agent/lookups")]
[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.AGENT)}")]
public sealed class LookupsController : ControllerBase
{
    private readonly ILookupService _lookups;

    public LookupsController(ILookupService lookups) => _lookups = lookups;

    [HttpGet("status-lookup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StatusLookupItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StatusLookupItemDto>>>> GetStatusLookups(CancellationToken ct)
    {
        var items = await _lookups.GetActiveStatusLookupsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<StatusLookupItemDto>>.Ok(items));
    }

    [HttpGet("query-status-lookup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QueryStatusLookupItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QueryStatusLookupItemDto>>>> GetQueryStatusLookups(CancellationToken ct)
    {
        var items = await _lookups.GetActiveQueryStatusLookupsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<QueryStatusLookupItemDto>>.Ok(items));
    }
}
