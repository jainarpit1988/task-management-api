using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations;

public partial class AddSoftDeleteColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddIsDeleted(migrationBuilder, "excel_uploads");
        AddIsDeleted(migrationBuilder, "excel_upload_errors");
        AddIsDeleted(migrationBuilder, "report_exports");
        AddIsDeleted(migrationBuilder, "task_acknowledgements");
        AddIsDeleted(migrationBuilder, "task_assignments");
        AddIsDeleted(migrationBuilder, "task_updates");
        AddIsDeleted(migrationBuilder, "task_status_history");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropIsDeleted(migrationBuilder, "task_status_history");
        DropIsDeleted(migrationBuilder, "task_updates");
        DropIsDeleted(migrationBuilder, "task_assignments");
        DropIsDeleted(migrationBuilder, "task_acknowledgements");
        DropIsDeleted(migrationBuilder, "report_exports");
        DropIsDeleted(migrationBuilder, "excel_upload_errors");
        DropIsDeleted(migrationBuilder, "excel_uploads");
    }

    private static void AddIsDeleted(MigrationBuilder migrationBuilder, string table) =>
        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            table: table,
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

    private static void DropIsDeleted(MigrationBuilder migrationBuilder, string table) =>
        migrationBuilder.DropColumn(name: "is_deleted", table: table);
}
