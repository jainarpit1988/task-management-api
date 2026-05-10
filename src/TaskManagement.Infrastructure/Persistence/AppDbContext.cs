using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskUpdate> TaskUpdates => Set<TaskUpdate>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskAcknowledgement> TaskAcknowledgements => Set<TaskAcknowledgement>();
    public DbSet<ExcelUpload> ExcelUploads => Set<ExcelUpload>();
    public DbSet<ExcelUploadError> ExcelUploadErrors => Set<ExcelUploadError>();
    public DbSet<ReportExport> ReportExports => Set<ReportExport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            e.Property(x => x.Mobile).HasColumnName("mobile").HasMaxLength(20);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasConversion<string>().IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Mobile).IsUnique();
        });

        modelBuilder.Entity<UserSession>(e =>
        {
            e.ToTable("user_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Token).HasColumnName("token").HasMaxLength(500).IsRequired();
            e.Property(x => x.DeviceInfo).HasColumnName("device_info").HasMaxLength(255);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // table has no updated_at
            e.Ignore(x => x.UpdatedAt);

            e.HasIndex(x => new { x.UserId, x.Token }).HasDatabaseName("idx_user_token");

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(100);
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(50);
            e.Property(x => x.EntityId).HasColumnName("entity_id");
            e.Property(x => x.OldValue).HasColumnName("old_value").HasColumnType("json");
            e.Property(x => x.NewValue).HasColumnName("new_value").HasColumnType("json");
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Ignore(x => x.UpdatedAt);

            e.HasIndex(x => x.UserId).HasDatabaseName("user_id");
            e.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("idx_entity");

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InternalId).HasColumnName("internal_id").HasMaxLength(50).IsRequired();
            e.Property(x => x.SrNo).HasColumnName("sr_no");
            e.Property(x => x.HubName).HasColumnName("hub_name").HasMaxLength(100);
            e.Property(x => x.ApplicationNo).HasColumnName("application_no").HasMaxLength(100);
            e.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(255);
            e.Property(x => x.EntityName).HasColumnName("entity_name").HasMaxLength(255);
            e.Property(x => x.LoanType).HasColumnName("loan_type").HasMaxLength(100);
            e.Property(x => x.LoanAmount).HasColumnName("loan_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.CustomerAddress).HasColumnName("customer_address");
            e.Property(x => x.Location).HasColumnName("location").HasMaxLength(255);
            e.Property(x => x.BranchHub).HasColumnName("branch_hub").HasMaxLength(100);
            e.Property(x => x.MobileNo).HasColumnName("mobile_no").HasMaxLength(20);

            e.Property(x => x.Status).HasColumnName("current_status").HasConversion<string>().IsRequired();
            e.Property(x => x.AssignedAgentId).HasColumnName("current_agent_id");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.LastUpdateId).HasColumnName("latest_followup_id");
            e.Property(x => x.Acknowledged).HasColumnName("acknowledged");
            e.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(x => x.RawData).HasColumnName("raw_data").HasColumnType("json");
            e.Property(x => x.CreatedFromUploadId).HasColumnName("uploaded_file_id");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.InternalId).IsUnique();
            e.HasIndex(x => x.ApplicationNo);
            e.HasIndex(x => x.AssignedAgentId);

            e.HasOne(x => x.AssignedAgent)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(x => x.AssignedAgentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedFromUpload)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(x => x.CreatedFromUploadId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.LastUpdate)
                .WithMany()
                .HasForeignKey(x => x.LastUpdateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskUpdate>(e =>
        {
            // Task updates are stored in task_updates (legacy schema used by current DB.sql).
            // Keep mapping compatible with the columns that exist in production.
            e.ToTable("task_updates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.Comment).HasColumnName("comment");

            e.Property(x => x.MeetingPersonMobile).HasColumnName("meeting_person_mobile").HasMaxLength(20);
            e.Property(x => x.FollowupDate).HasColumnName("followup_date").HasConversion<DateOnlyConverter, DateOnlyComparer>();
            e.Property(x => x.MeetingPersonName).HasColumnName("meeting_person_name").HasMaxLength(255);

            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // DB.sql table has no updated_at column; some envs may, but it's not required.
            e.Ignore(x => x.UpdatedAt);

            // Columns not present in DB.sql (safe to ignore for compatibility).
            e.Ignore(x => x.VisitDate);
            e.Ignore(x => x.NextFollowupDate);
            e.Ignore(x => x.FollowupNotes);
            e.Ignore(x => x.Latitude);
            e.Ignore(x => x.Longitude);
            e.Ignore(x => x.AttachmentUrl);

            e.HasOne(x => x.Task)
                .WithMany(t => t.Updates)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Agent)
                .WithMany(u => u.TaskUpdates)
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskAssignment>(e =>
        {
            e.ToTable("task_assignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.AssignedBy).HasColumnName("assigned_by");
            // New schema column is assigned_date (not assigned_at)
            e.Property(x => x.AssignedAt).HasColumnName("assigned_date");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // DB table has no updated_at column
            e.Ignore(x => x.UpdatedAt);

            e.HasOne(x => x.Task)
                .WithMany(t => t.Assignments)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Agent)
                .WithMany()
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedByUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskAcknowledgement>(e =>
        {
            e.ToTable("task_acknowledgements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TaskId).HasColumnName("task_id");
            e.Property(x => x.AgentId).HasColumnName("agent_id");
            e.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // DB table has no updated_at column
            e.Ignore(x => x.UpdatedAt);

            e.HasOne(x => x.Task)
                .WithMany(t => t.Acknowledgements)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Agent)
                .WithMany()
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExcelUpload>(e =>
        {
            e.ToTable("excel_uploads");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
            e.Property(x => x.FilePath).HasColumnName("file_path").HasMaxLength(500);
            e.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
            e.Property(x => x.TotalRows).HasColumnName("total_rows");
            e.Property(x => x.SuccessRows).HasColumnName("success_rows");
            e.Property(x => x.FailedRows).HasColumnName("failed_rows");
            // Production schema stores status as ENUM strings.
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExcelUploadError>(e =>
        {
            e.ToTable("excel_upload_errors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UploadId).HasColumnName("upload_id");
            e.Property(x => x.ExcelRowNumber).HasColumnName("excel_row_number");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message").IsRequired();
            e.Property(x => x.RawData).HasColumnName("raw_data").HasColumnType("json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // DB table has no updated_at column
            e.Ignore(x => x.UpdatedAt);

            e.HasOne(x => x.Upload)
                .WithMany(u => u.Errors)
                .HasForeignKey(x => x.UploadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportExport>(e =>
        {
            e.ToTable("report_exports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
            e.Property(x => x.FilePath).HasColumnName("file_path").HasMaxLength(500);
            e.Property(x => x.GeneratedBy).HasColumnName("generated_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // DB table has no updated_at column
            e.Ignore(x => x.UpdatedAt);

            e.HasOne(x => x.GeneratedByUser)
                .WithMany()
                .HasForeignKey(x => x.GeneratedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

