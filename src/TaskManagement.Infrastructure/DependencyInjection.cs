using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.Interfaces.Repositories;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Repositories;
using TaskManagement.Infrastructure.Security;
using TaskManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddDbContext<AppDbContext>(opt =>
        {
            var conn = config.GetConnectionString("Default");
            opt.UseMySql(conn, ServerVersion.AutoDetect(conn));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskUpdateRepository, TaskUpdateRepository>();
        services.AddScoped<ITaskAssignmentRepository, TaskAssignmentRepository>();
        services.AddScoped<ITaskAcknowledgementRepository, TaskAcknowledgementRepository>();
        services.AddScoped<IExcelUploadRepository, ExcelUploadRepository>();
        services.AddScoped<IExcelUploadErrorRepository, ExcelUploadErrorRepository>();
        services.AddScoped<IReportExportRepository, ReportExportRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<ILookupService, LookupService>();

        return services;
    }
}

