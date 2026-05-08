using AutoMapper;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using TaskManagement.Application.Common.Exceptions;
using TaskManagement.Application.DTOs;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Helpers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Infrastructure.Security;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUserSessionRepository _sessions;
    private readonly IAuditLogRepository _audit;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _http;

    public AuthService(
        IUserRepository users,
        IUserSessionRepository sessions,
        IAuditLogRepository audit,
        IJwtTokenGenerator jwt,
        IMapper mapper,
        IHttpContextAccessor http)
    {
        _users = users;
        _sessions = sessions;
        _audit = audit;
        _jwt = jwt;
        _mapper = mapper;
        _http = http;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct)
    {
        var user = await _users.FindByEmailOrMobileAsync(request.EmailOrMobile, ct)
                   ?? throw new AppException("Invalid credentials", 401);

        if (user.IsDeleted || user.Status != TaskManagement.Domain.Enums.UserStatus.ACTIVE)
            throw new AppException("User is inactive", 403);

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
            throw new AppException("Invalid credentials", 401);

        var (token, expiresAtUtc) = _jwt.CreateToken(user);

        // Persist session for auditing/revocation later
        var deviceInfo = _http.HttpContext?.Request.Headers.UserAgent.ToString();
        var tokenHash = ComputeSha256Hex(token);
        await _sessions.AddAsync(new UserSession
        {
            UserId = user.Id,
            // Never store raw JWT in DB; it can exceed column size and is a credential.
            // Store a stable hash for lookup/revocation/auditing.
            Token = tokenHash,
            DeviceInfo = string.IsNullOrWhiteSpace(deviceInfo) ? null : deviceInfo,
            ExpiresAt = expiresAtUtc
        }, ct);

        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        await _audit.AddAsync(new AuditLog
        {
            UserId = user.Id,
            Action = "LOGIN",
            EntityType = "users",
            EntityId = user.Id,
            OldValue = null,
            NewValue = null,
            IpAddress = ip
        }, ct);

        await _sessions.SaveChangesAsync(ct);
        await _audit.SaveChangesAsync(ct);

        return new LoginResponseDto
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            User = _mapper.Map<UserDto>(user)
        };
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant(); // 64 chars
    }
}

