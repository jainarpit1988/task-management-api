using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public long UserId
    {
        get
        {
            var id = _http.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(id, out var userId) ? userId : 0;
        }
    }

    public UserRole Role
    {
        get
        {
            var role = _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(role, out var parsed) ? parsed : UserRole.AGENT;
        }
    }

    public string? Email => _http.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
    public string? Mobile => _http.HttpContext?.User?.FindFirst("mobile")?.Value;
}

