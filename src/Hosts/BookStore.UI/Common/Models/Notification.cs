namespace BookStore.UI.Common.Models;

public record Notification
{
    public required Guid Id { get; init; }
    public required string Message { get; init; }
    public required bool IsSuccess { get; init; }
    public required bool AutoHide { get; init; }
}
