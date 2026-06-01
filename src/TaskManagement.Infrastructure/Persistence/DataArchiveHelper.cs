using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public static async Task<IReadOnlyList<string>> ResolveArchiveTablesAsync(DbContext db, CancellationToken ct)
    {
        var tables = new List<string>();
        foreach (var table in ArchiveTableNames)
        {
            if (await TableExistsAsync(db, table, ct))
                tables.Add(table);
        }

        var followupsTable = await ResolveTaskFollowupsTableNameAsync(db, ct);
        if (await TableExistsAsync(db, followupsTable, ct) &&
            !tables.Contains(followupsTable, StringComparer.OrdinalIgnoreCase))
        {
            tables.Add(followupsTable);
        }

        return tables;
    }

    public static async Task<bool> TableExistsAsync(DbContext db, string tableName, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var cmd = connection.CreateCommand();
            AttachCurrentTransaction(db, cmd);
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
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AttachCurrentTransaction(DbContext db, DbCommand cmd)
    {
        var txn = db.Database.CurrentTransaction;
        if (txn is not null)
            cmd.Transaction = txn.GetDbTransaction();
    }
}
