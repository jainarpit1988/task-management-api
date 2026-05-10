namespace TaskManagement.Domain.Enums;

public enum TaskStatus
{
    // "Open" bucket. Kept compatible with older persisted value "NEW".
    OPEN = 1,
    NEW = OPEN,

    // "In progress" bucket. Kept compatible with older persisted value "PENDING".
    IN_PROGRESS = 2,
    PENDING = IN_PROGRESS,

    VISITED = 3,
    NOT_INTERESTED = 4,
    CONVERTED = 5,
    FOLLOW_UP_REQUIRED = 6,
    CLOSED = 7
}

