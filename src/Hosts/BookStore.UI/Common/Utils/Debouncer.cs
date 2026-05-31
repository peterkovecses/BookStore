namespace BookStore.UI.Common.Utils;

/// <summary>
/// Provides a mechanism to debounce actions, ensuring they are only executed after a specified delay.
/// </summary>
public sealed class Debouncer(TimeProvider timeProvider) : IDisposable
{
    private ITimer? _timer;
    private Action? _action;

    public Debouncer() : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Debounces the specified action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="milliseconds">The delay in milliseconds.</param>
    public void Debounce(Action action, int milliseconds)
    {
        _action = action;
        _timer ??= timeProvider.CreateTimer(_ => _action?.Invoke(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(TimeSpan.FromMilliseconds(milliseconds), Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Cancels any pending debounced action.
    /// </summary>
    public void Cancel()
    {
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _action = null;
    }

    public void Dispose() => _timer?.Dispose();
}
