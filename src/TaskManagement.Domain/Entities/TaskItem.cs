using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Entities;

public class TaskItem : AuditableEntity
{
    public long Id { get; set; }

    public string InternalId { get; set; } = null!;

    // Excel columns A-K
    public int? SrNo { get; set; }
    public string? HubName { get; set; }
    public string? ApplicationNo { get; set; }
    public string? CustomerName { get; set; }
    public string? EntityName { get; set; }
    public string? LoanType { get; set; }
    public decimal? LoanAmount { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Location { get; set; }
    public string? BranchHub { get; set; }
    public string? MobileNo { get; set; }

    // Current task state
    public TaskStatus Status { get; set; } = TaskStatus.NEW; // maps to current_status
    public long? AssignedAgentId { get; set; } // maps to current_agent_id
    public User? AssignedAgent { get; set; }

    public DateTime? DueDate { get; set; } // maps to due_date

    public long? LastUpdateId { get; set; } // maps to latest_followup_id
    public TaskUpdate? LastUpdate { get; set; }

    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public string? RawData { get; set; } // stored as JSON string

    public long? CreatedFromUploadId { get; set; } // maps to uploaded_file_id
    public ExcelUpload? CreatedFromUpload { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<TaskUpdate> Updates { get; set; } = new List<TaskUpdate>();
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
    public ICollection<TaskAcknowledgement> Acknowledgements { get; set; } = new List<TaskAcknowledgement>();
}

