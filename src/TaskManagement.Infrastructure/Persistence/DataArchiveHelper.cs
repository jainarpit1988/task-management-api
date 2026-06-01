using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Infrastructure.Persistence;

internal static class DataArchiveHelper
{
    public static readonly string[] ArchiveTableNames =
    {
        "excel_upload_errors",
        "excel_uploads",
        "report_exports",
        "task_acknowledgements",
        "task_assignments",
        "task_status_history",
        "tasks"
    };

    public static async Task<string> ResolveTaskFollowupsTableNameAsync(DbContext db, CancellationToken ct)
    {
        if (await TableExistsAsync(db, "task_followups", ct))
            return "task_followups";

        if (await TableExistsAsync(db, "task_updates", ct))
            return "task_updates";

        return "task_updates";
    }

    public static async Task<bool> TableExistsAsync(DbContext db, string tableName, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = """
                                SELECT COUNT(*)
                                FROM information_schema.TABLES
                                WHERE TABLE_SCHEMA = DATABASE()
                                  AND TABLE_NAME = @name
                                """;
            var p = cmd.CreateParameter();
            p.ParameterName = "@name";
            p.Value = tableName;
            cmd.Parameters.Add(p);
            var scalar = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(scalar) > 0;
        }
        finally
        {
            try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }
        }
    }
}
