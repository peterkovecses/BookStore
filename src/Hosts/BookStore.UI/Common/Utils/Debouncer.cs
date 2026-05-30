namespace BookStore.UI.Common.Utils;

/// <summary>
/// Provides a mechanism to debounce actions, ensuring they are only executed after a specified delay.
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private Action? _action;

    public Debouncer()
    {
        _timer = new System.Timers.Timer
        {
            AutoReset = false
        };
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>
    /// Debounces the specified action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="milliseconds">The delay in milliseconds.</param>
    public void Debounce(Action action, int milliseconds)
    {
        _action = action;
        _timer.Stop();
        _timer.Interval = milliseconds;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _action?.Invoke();
    }

    public void Dispose()
    {
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
    }
}
