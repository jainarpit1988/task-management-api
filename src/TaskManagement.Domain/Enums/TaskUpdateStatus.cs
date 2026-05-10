namespace TaskManagement.Domain.Enums;

public enum TaskUpdateStatus
{
    PENDING = 1,
    IN_PROGRESS = PENDING,
    VISITED = 2,
    NOT_INTERESTED = 3,
    CONVERTED = 4,
    FOLLOW_UP_REQUIRED = 5,
    CLOSED = 6
}

