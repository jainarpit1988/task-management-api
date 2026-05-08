using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Data;
using Serilog;
using TaskManagement.Application.Mapping;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Security;
using TaskManagement.Api.Middleware;
using TaskManagement.Api.Background;
using TaskManagement.Application.Validation.Admin;
using TaskManagement.Application.Validation.Agent;
using TaskManagement.Application.Validation.Auth;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Upload limits (multipart/form-data)
// Keep in sync with IIS requestFiltering / Kestrel limits in hosting config.
const long maxUploadBytes = 100L * 1024 * 1024; // 100 MB

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxUploadBytes;
});

builder.Services.Configure<IISServerOptions>(o =>
{
    o.MaxRequestBodySize = maxUploadBytes;
});

builder.Services.AddControllers();
builder.Services.AddHostedService<ExcelUploadWorker>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(AppMappingProfile));

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateAgentRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<AddTaskUpdateRequestValidator>();

// Infrastructure (DbContext, repos, services)
builder.Services.AddInfrastructure(builder.Configuration);

// JWT auth
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// Swagger (JWT support)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Task Management API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {your JWT token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { scheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSchemaGuard");

    // Production DB can drift from EF model if migrations/scripts weren't applied.
    // This guard prevents hard 500s like: "Unknown column 'updated_at' in 'field list'".
    async Task EnsureColumnAsync(string table, string column, string ddl)
    {
        try
        {
            await db.Database.OpenConnectionAsync();
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                              SELECT COUNT(*)
                              FROM information_schema.COLUMNS
                              WHERE TABLE_SCHEMA = DATABASE()
                                AND TABLE_NAME = @table
                                AND COLUMN_NAME = @column
                              """;

            var pTable = cmd.CreateParameter();
            pTable.ParameterName = "@table";
            pTable.Value = table;
            cmd.Parameters.Add(pTable);

            var pColumn = cmd.CreateParameter();
            pColumn.ParameterName = "@column";
            pColumn.Value = column;
            cmd.Parameters.Add(pColumn);

            var scalar = await cmd.ExecuteScalarAsync();
            var count = Convert.ToInt64(scalar);
            if (count > 0) return;

            logger.LogWarning("DB schema drift detected. Adding missing column {Table}.{Column}", table, column);
            await db.Database.ExecuteSqlRawAsync(ddl);
            logger.LogInformation("Added missing column {Table}.{Column}", table, column);
        }
        catch (Exception ex)
        {
            // Don't crash startup; but log loudly so it can be fixed permanently via migrations.
            logger.LogError(ex, "Failed ensuring column {Table}.{Column}", table, column);
        }
        finally
        {
            try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }
        }
    }

    async Task EnsureExcelUploadStatusEnumHasQueuedAsync()
    {
        try
        {
            await db.Database.OpenConnectionAsync();
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = """
                              SELECT COLUMN_TYPE
                              FROM information_schema.COLUMNS
                              WHERE TABLE_SCHEMA = DATABASE()
                                AND TABLE_NAME = 'excel_uploads'
                                AND COLUMN_NAME = 'status'
                              LIMIT 1
                              """;

            var scalar = await cmd.ExecuteScalarAsync();
            var columnType = (scalar?.ToString()) ?? string.Empty;

            // Example: enum('PROCESSING','COMPLETED','FAILED')
            if (columnType.Contains("'QUEUED'", StringComparison.OrdinalIgnoreCase))
                return;

            logger.LogWarning("DB schema drift detected. Updating excel_uploads.status enum to include QUEUED. current={ColumnType}", columnType);

            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE `excel_uploads` MODIFY COLUMN `status` enum('QUEUED','PROCESSING','COMPLETED','FAILED') COLLATE utf8mb3_unicode_ci DEFAULT 'PROCESSING';");

            logger.LogInformation("Updated excel_uploads.status enum to include QUEUED");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed ensuring excel_uploads.status enum includes QUEUED");
        }
        finally
        {
            try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }
        }
    }

    // Minimum fix for current 500: excel_uploads.updated_at is missing in prod.
    // Keep nullable to be safe across existing rows.
    await EnsureColumnAsync(
        table: "excel_uploads",
        column: "updated_at",
        ddl: "ALTER TABLE `excel_uploads` ADD COLUMN `updated_at` datetime(6) NULL;");

    await EnsureExcelUploadStatusEnumHasQueuedAsync();

    // Recovery: if the app recycles mid-processing, re-enqueue stuck uploads.
    try
    {
        var pendingIds = await db.ExcelUploads.AsNoTracking()
            .Where(x => x.Status == TaskManagement.Domain.Enums.ExcelUploadStatus.QUEUED ||
                        x.Status == TaskManagement.Domain.Enums.ExcelUploadStatus.PROCESSING)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(50)
            .ToListAsync();

        foreach (var id in pendingIds)
        {
            TaskManagement.Infrastructure.Services.ExcelUploadBackgroundQueue.Enqueue(id);
            logger.LogWarning("Re-enqueued pending excel upload. uploadId={UploadId}", id);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to re-enqueue pending excel uploads on startup");
    }

    await AppDbContextSeed.SeedAsync(db, CancellationToken.None);
}

app.Run();

