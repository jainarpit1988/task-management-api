using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManagement.Infrastructure.Persistence;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasCharSet("utf8mb4")
            .UseCollation("utf8mb4_general_ci");

        modelBuilder.Entity("TaskManagement.Domain.Entities.ExcelUpload", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<string>("FileName").HasMaxLength(255).HasColumnName("file_name").HasColumnType("varchar(255)");
            b.Property<string>("FilePath").HasMaxLength(500).HasColumnName("file_path").HasColumnType("varchar(500)");
            b.Property<int>("FailedRows").HasColumnName("failed_rows").HasColumnType("int");
            b.Property<int>("SuccessRows").HasColumnName("success_rows").HasColumnType("int");
            b.Property<string>("Status").IsRequired().HasColumnName("status").HasColumnType("longtext");
            b.Property<int>("TotalRows").HasColumnName("total_rows").HasColumnType("int");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");
            b.Property<long?>("UploadedBy").HasColumnName("uploaded_by").HasColumnType("bigint");

            b.HasKey("Id");
            b.HasIndex("UploadedBy");
            b.ToTable("excel_uploads");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.ExcelUploadError", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<string>("ErrorMessage").IsRequired().HasColumnName("error_message").HasColumnType("longtext");
            b.Property<int?>("ExcelRowNumber").HasColumnName("excel_row_number").HasColumnType("int");
            b.Property<string>("RawData").HasColumnName("raw_data").HasColumnType("json");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");
            b.Property<long?>("UploadId").HasColumnName("upload_id").HasColumnType("bigint");

            b.HasKey("Id");
            b.HasIndex("UploadId");
            b.ToTable("excel_upload_errors");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.ReportExport", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<string>("FileName").HasMaxLength(255).HasColumnName("file_name").HasColumnType("varchar(255)");
            b.Property<string>("FilePath").HasMaxLength(500).HasColumnName("file_path").HasColumnType("varchar(500)");
            b.Property<long?>("GeneratedBy").HasColumnName("generated_by").HasColumnType("bigint");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");

            b.HasKey("Id");
            b.HasIndex("GeneratedBy");
            b.ToTable("report_exports");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskAcknowledgement", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<long>("AgentId").HasColumnName("agent_id").HasColumnType("bigint");
            b.Property<DateTime>("AcknowledgedAt").HasColumnName("acknowledged_at").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<long>("TaskId").HasColumnName("task_id").HasColumnType("bigint");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");

            b.HasKey("Id");
            b.HasIndex("AgentId");
            b.HasIndex("TaskId");
            b.ToTable("task_acknowledgements");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskAssignment", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<long>("AgentId").HasColumnName("agent_id").HasColumnType("bigint");
            b.Property<DateTime>("AssignedAt").HasColumnName("assigned_at").HasColumnType("datetime(6)");
            b.Property<long?>("AssignedBy").HasColumnName("assigned_by").HasColumnType("bigint");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<long>("TaskId").HasColumnName("task_id").HasColumnType("bigint");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");

            b.HasKey("Id");
            b.HasIndex("AgentId");
            b.HasIndex("AssignedBy");
            b.HasIndex("TaskId");
            b.ToTable("task_assignments");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskItem", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<bool>("Acknowledged").HasColumnName("acknowledged").HasColumnType("tinyint(1)");
            b.Property<DateTime?>("AcknowledgedAt").HasColumnName("acknowledged_at").HasColumnType("datetime(6)");
            b.Property<string>("ApplicationNo").HasMaxLength(100).HasColumnName("application_no").HasColumnType("varchar(100)");
            b.Property<long?>("AssignedAgentId").HasColumnName("assigned_agent_id").HasColumnType("bigint");
            b.Property<DateTime?>("AssignedDate").HasColumnName("assigned_date").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<long?>("CreatedFromUploadId").HasColumnName("created_from_upload_id").HasColumnType("bigint");
            b.Property<string>("CustomerAddress").HasColumnName("customer_address").HasColumnType("longtext");
            b.Property<string>("CustomerMobile").HasMaxLength(20).HasColumnName("customer_mobile").HasColumnType("varchar(20)");
            b.Property<string>("CustomerName").HasMaxLength(150).HasColumnName("customer_name").HasColumnType("varchar(150)");
            b.Property<DateTime?>("DueDate").HasColumnName("due_date").HasColumnType("datetime(6)");
            b.Property<string>("InternalId").IsRequired().HasMaxLength(50).HasColumnName("internal_id").HasColumnType("varchar(50)");
            b.Property<bool>("IsDeleted").HasColumnName("is_deleted").HasColumnType("tinyint(1)");
            b.Property<long?>("LastUpdateId").HasColumnName("last_update_id").HasColumnType("bigint");
            b.Property<string>("RawData").HasColumnName("raw_data").HasColumnType("json");
            b.Property<string>("Status").IsRequired().HasColumnName("status").HasColumnType("longtext");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");

            b.HasKey("Id");
            b.HasIndex("ApplicationNo");
            b.HasIndex("AssignedAgentId");
            b.HasIndex("CreatedFromUploadId");
            b.HasIndex("InternalId").IsUnique();
            b.HasIndex("LastUpdateId");
            b.ToTable("tasks");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskUpdate", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<long>("AgentId").HasColumnName("agent_id").HasColumnType("bigint");
            b.Property<string>("Comment").HasColumnName("comment").HasColumnType("longtext");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<DateTime?>("FollowupDate").HasColumnName("followup_date").HasColumnType("datetime(6)");
            b.Property<string>("MeetingPersonMobile").HasMaxLength(20).HasColumnName("meeting_person_mobile").HasColumnType("varchar(20)");
            b.Property<string>("MeetingPersonName").HasMaxLength(150).HasColumnName("meeting_person_name").HasColumnType("varchar(150)");
            b.Property<string>("Status").IsRequired().HasColumnName("status").HasColumnType("longtext");
            b.Property<long>("TaskId").HasColumnName("task_id").HasColumnType("bigint");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");

            b.HasKey("Id");
            b.HasIndex("AgentId");
            b.HasIndex("TaskId");
            b.ToTable("task_updates");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.User", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnName("id").HasColumnType("bigint");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("datetime(6)");
            b.Property<string>("Email").HasMaxLength(150).HasColumnName("email").HasColumnType("varchar(150)");
            b.Property<bool>("IsDeleted").HasColumnName("is_deleted").HasColumnType("tinyint(1)");
            b.Property<string>("Mobile").HasMaxLength(20).HasColumnName("mobile").HasColumnType("varchar(20)");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnName("name").HasColumnType("varchar(100)");
            b.Property<string>("PasswordHash").IsRequired().HasMaxLength(255).HasColumnName("password_hash").HasColumnType("varchar(255)");
            b.Property<string>("Role").IsRequired().HasColumnName("role").HasColumnType("longtext");
            b.Property<string>("Status").IsRequired().HasColumnName("status").HasColumnType("longtext");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("datetime(6)");

            b.HasKey("Id");
            b.HasIndex("Email").IsUnique();
            b.HasIndex("Mobile").IsUnique();
            b.ToTable("users");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.ExcelUpload", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.User", "UploadedByUser")
                .WithMany()
                .HasForeignKey("UploadedBy")
                .OnDelete(DeleteBehavior.Restrict);

            b.Navigation("UploadedByUser");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.ExcelUploadError", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.ExcelUpload", "Upload")
                .WithMany("Errors")
                .HasForeignKey("UploadId")
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation("Upload");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.ReportExport", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.User", "GeneratedByUser")
                .WithMany()
                .HasForeignKey("GeneratedBy")
                .OnDelete(DeleteBehavior.Restrict);

            b.Navigation("GeneratedByUser");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskAcknowledgement", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.User", "Agent")
                .WithMany()
                .HasForeignKey("AgentId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TaskManagement.Domain.Entities.TaskItem", "Task")
                .WithMany("Acknowledgements")
                .HasForeignKey("TaskId")
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation("Agent");
            b.Navigation("Task");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskAssignment", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.User", "Agent")
                .WithMany()
                .HasForeignKey("AgentId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TaskManagement.Domain.Entities.User", "AssignedByUser")
                .WithMany()
                .HasForeignKey("AssignedBy")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TaskManagement.Domain.Entities.TaskItem", "Task")
                .WithMany("Assignments")
                .HasForeignKey("TaskId")
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation("Agent");
            b.Navigation("AssignedByUser");
            b.Navigation("Task");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskItem", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.User", "AssignedAgent")
                .WithMany("AssignedTasks")
                .HasForeignKey("AssignedAgentId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TaskManagement.Domain.Entities.ExcelUpload", "CreatedFromUpload")
                .WithMany("CreatedTasks")
                .HasForeignKey("CreatedFromUploadId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TaskManagement.Domain.Entities.TaskUpdate", "LastUpdate")
                .WithMany()
                .HasForeignKey("LastUpdateId")
                .OnDelete(DeleteBehavior.Restrict);

            b.Navigation("AssignedAgent");
            b.Navigation("CreatedFromUpload");
            b.Navigation("LastUpdate");
        });

        modelBuilder.Entity("TaskManagement.Domain.Entities.TaskUpdate", b =>
        {
            b.HasOne("TaskManagement.Domain.Entities.User", "Agent")
                .WithMany("TaskUpdates")
                .HasForeignKey("AgentId")
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne("TaskManagement.Domain.Entities.TaskItem", "Task")
                .WithMany("Updates")
                .HasForeignKey("TaskId")
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation("Agent");
            b.Navigation("Task");
        });
    }
}

