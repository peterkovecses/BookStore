namespace BookStore.UI.Common.Services;

public class NotificationService
{
    public event Action<Notification>? OnNotificationAdded;
    public event Action? OnNotificationsCleared;

    public void ShowSuccess(string message)
    {
        OnNotificationAdded?.Invoke(new Notification
        {
            Id = Guid.NewGuid(),
            Message = message,
            IsSuccess = true,
            AutoHide = true
        });
    }

    public void ShowFailure(string message = "Something went wrong, please try again later.")
    {
        OnNotificationAdded?.Invoke(new Notification
        {
            Id = Guid.NewGuid(),
            Message = message,
            IsSuccess = false,
            AutoHide = false
        });
    }

    public void ClearNotifications()
    {
        OnNotificationsCleared?.Invoke();
    }
}
