namespace TaskManagement.Domain.Enums;

public enum TaskStatus
{
    // IMPORTANT: EF persists enums as strings (HasConversion<string>).
    // The first name for a numeric value is what gets stored via ToString().
    // Keep legacy DB values ("NEW", "PENDING") as the primary names, while
    // providing friendlier aliases ("OPEN", "IN_PROGRESS") for app/UI usage.
    NEW = 1,
    OPEN = NEW,

    PENDING = 2,
    IN_PROGRESS = PENDING,

    VISITED = 3,
    NOT_INTERESTED = 4,
    CONVERTED = 5,
    FOLLOW_UP_REQUIRED = 6,
    CLOSED = 7
}

