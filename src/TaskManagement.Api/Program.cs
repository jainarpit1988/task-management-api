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
using System.Text.Json.Serialization;
using Serilog;
using TaskManagement.Api.Serialization;
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

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new IstDateTimeJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new IstNullableDateTimeJsonConverter());
        // Send/receive enums as strings to avoid UI-index -> enum-number mismatches
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    });
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

    async Task EnsureTaskCurrentStatusEnumIsCompatibleAsync()
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
                                AND TABLE_NAME = 'tasks'
                                AND COLUMN_NAME = 'current_status'
                              LIMIT 1
                              """;

            var scalar = await cmd.ExecuteScalarAsync();
            var columnType = (scalar?.ToString()) ?? string.Empty;

            // If it's not an enum, there's nothing to patch here.
            if (!columnType.StartsWith("enum(", StringComparison.OrdinalIgnoreCase))
                return;

            // We persist TaskStatus as strings; these must be accepted by the DB enum.
            // Keep the legacy values the app uses (NEW/PENDING/...) to avoid "Data truncated".
            var required = new[]
            {
                "NEW",
                "PENDING",
                "VISITED",
                "NOT_INTERESTED",
                "CONVERTED",
                "FOLLOW_UP_REQUIRED",
                "CLOSED"
            };

            var missing = required.Where(v => !columnType.Contains($"'{v}'", StringComparison.OrdinalIgnoreCase)).ToList();
            if (missing.Count == 0)
                return;

            logger.LogWarning(
                "DB schema drift detected. Updating tasks.current_status enum to include required values. missing={Missing} current={ColumnType}",
                string.Join(",", missing),
                columnType);

            // Expand/normalize enum list to required set (order doesn't matter for enum type).
            var enumSql = "enum(" + string.Join(",", required.Select(v => $"'{v}'")) + ")";

#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE `tasks` MODIFY COLUMN `current_status` {enumSql} COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT 'NEW';");
#pragma warning restore EF1002

            logger.LogInformation("Updated tasks.current_status enum for compatibility");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed ensuring tasks.current_status enum is compatible");
        }
        finally
        {
            try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }
        }
    }

    async Task EnsureAutoIncrementAsync(string table, string idColumn = "id")
    {
        try
        {
            await db.Database.OpenConnectionAsync();

            // 1) Ensure the PK column is AUTO_INCREMENT (DB drift safety).
            await using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = """
                                  SELECT EXTRA
                                  FROM information_schema.COLUMNS
                                  WHERE TABLE_SCHEMA = DATABASE()
                                    AND TABLE_NAME = @table
                                    AND COLUMN_NAME = @column
                                  LIMIT 1
                                  """;

                var pTable = cmd.CreateParameter();
                pTable.ParameterName = "@table";
                pTable.Value = table;
                cmd.Parameters.Add(pTable);

                var pColumn = cmd.CreateParameter();
                pColumn.ParameterName = "@column";
                pColumn.Value = idColumn;
                cmd.Parameters.Add(pColumn);

                var extra = (await cmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;
                if (!extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("DB schema drift detected. Enabling AUTO_INCREMENT on {Table}.{Column}. extra={Extra}", table, idColumn, extra);
#pragma warning disable EF1002
                    await db.Database.ExecuteSqlRawAsync($"ALTER TABLE `{table}` MODIFY COLUMN `{idColumn}` BIGINT NOT NULL AUTO_INCREMENT;"); // table/column are internal constants
#pragma warning restore EF1002
                    logger.LogInformation("Enabled AUTO_INCREMENT on {Table}.{Column}", table, idColumn);
                }
            }

            // 2) Ensure AUTO_INCREMENT counter is ahead of MAX(id) to avoid duplicate PK inserts.
            long maxId;
            await using (var cmdMax = db.Database.GetDbConnection().CreateCommand())
            {
                cmdMax.CommandType = CommandType.Text;
                cmdMax.CommandText = $"SELECT IFNULL(MAX(`{idColumn}`), 0) FROM `{table}`;";
                var scalar = await cmdMax.ExecuteScalarAsync();
                maxId = Convert.ToInt64(scalar);
            }

            long? nextAuto;
            await using (var cmdAuto = db.Database.GetDbConnection().CreateCommand())
            {
                cmdAuto.CommandType = CommandType.Text;
                cmdAuto.CommandText = """
                                       SELECT AUTO_INCREMENT
                                       FROM information_schema.TABLES
                                       WHERE TABLE_SCHEMA = DATABASE()
                                         AND TABLE_NAME = @table
                                       LIMIT 1
                                       """;

                var pTable2 = cmdAuto.CreateParameter();
                pTable2.ParameterName = "@table";
                pTable2.Value = table;
                cmdAuto.Parameters.Add(pTable2);

                var scalar = await cmdAuto.ExecuteScalarAsync();
                nextAuto = scalar == null || scalar == DBNull.Value ? null : Convert.ToInt64(scalar);
            }

            // If info_schema doesn't expose it (rare), we can't repair safely here.
            if (nextAuto.HasValue && nextAuto.Value <= maxId)
            {
                var bumpTo = maxId + 1;
                logger.LogWarning(
                    "DB AUTO_INCREMENT drift detected. Bumping {Table} AUTO_INCREMENT from {AutoInc} to {BumpTo} (maxId={MaxId})",
                    table, nextAuto.Value, bumpTo, maxId);

#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE `{table}` AUTO_INCREMENT = {bumpTo};"); // table is internal constant; bumpTo is computed server-side
#pragma warning restore EF1002
                logger.LogInformation("Bumped {Table} AUTO_INCREMENT to {BumpTo}", table, bumpTo);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed ensuring AUTO_INCREMENT correctness for {Table}.{Column}", table, idColumn);
        }
        finally
        {
            try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }
        }
    }

    async Task EnsureSoftDeleteColumnsAsync()
    {
        var tables = new[]
        {
            "excel_upload_errors",
            "excel_uploads",
            "report_exports",
            "task_acknowledgements",
            "task_assignments",
            "task_status_history",
            "task_followups",
            "task_updates",
            "tasks"
        };

        foreach (var table in tables)
        {
            await EnsureColumnAsync(
                table,
                "is_deleted",
                $"ALTER TABLE `{table}` ADD COLUMN `is_deleted` tinyint(1) NOT NULL DEFAULT 0;");
        }
    }

    async Task EnsureTaskUpdatesCompatibilityAsync()
    {
        try
        {
            await db.Database.OpenConnectionAsync();

            async Task<bool> TableOrViewExistsAsync(string name)
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = """
                                  SELECT COUNT(*)
                                  FROM information_schema.TABLES
                                  WHERE TABLE_SCHEMA = DATABASE()
                                    AND TABLE_NAME = @name
                                  """;
                var p = cmd.CreateParameter();
                p.ParameterName = "@name";
                p.Value = name;
                cmd.Parameters.Add(p);
                var scalar = await cmd.ExecuteScalarAsync();
                return Convert.ToInt64(scalar) > 0;
            }

            var hasTaskUpdates = await TableOrViewExistsAsync("task_updates");
            if (hasTaskUpdates)
                return;

            var hasTaskFollowups = await TableOrViewExistsAsync("task_followups");
            if (!hasTaskFollowups)
                return;

            logger.LogWarning("DB schema drift detected. Creating view task_updates over task_followups for compatibility.");

            // Create an updatable view so EF (mapped to task_updates) can read/write.
            // Map: comments -> comment
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync("""
                                                 CREATE OR REPLACE VIEW `task_updates` AS
                                                 SELECT
                                                     `id`,
                                                     `task_id`,
                                                     `agent_id`,
                                                     `status`,
                                                     `comments` AS `comment`,
                                                     `meeting_person_name`,
                                                     `meeting_person_mobile`,
                                                     `followup_date`,
                                                     `created_at`,
                                                     `is_deleted`
                                                 FROM `task_followups`;
                                                 """);
#pragma warning restore EF1002

            logger.LogInformation("Created view task_updates for compatibility");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed ensuring task_updates/task_followups compatibility");
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
    await EnsureAutoIncrementAsync(table: "excel_uploads", idColumn: "id");
    await EnsureTaskCurrentStatusEnumIsCompatibleAsync();
    await EnsureSoftDeleteColumnsAsync();
    await EnsureTaskUpdatesCompatibilityAsync();

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

