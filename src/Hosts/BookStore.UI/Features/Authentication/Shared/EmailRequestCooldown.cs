namespace BookStore.UI.Features.Authentication;

internal sealed class EmailRequestCooldown(
    IJSRuntime js,
    TimeProvider clock,
    string storageKey,
    Func<Task> refresh) : IDisposable
{
    private readonly CancellationTokenSource _dispose = new();
    private DateTimeOffset? _until;

    internal int RemainingSeconds => _until is { } until
        ? Math.Max(0, (int)Math.Ceiling((until - clock.GetUtcNow()).TotalSeconds))
        : 0;

    internal async Task RestoreAsync()
    {
        var stored = await js.InvokeAsync<string?>("sessionStorage.getItem", storageKey);
        var now = clock.GetUtcNow();
        if (long.TryParse(stored, out var milliseconds) &&
            milliseconds > now.ToUnixTimeMilliseconds() &&
            milliseconds <= now.AddHours(1).ToUnixTimeMilliseconds())
        {
            _until = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            if (!_dispose.IsCancellationRequested) _ = UpdateAsync();
        }
    }

    internal async Task StartAsync(int seconds)
    {
        if (_dispose.IsCancellationRequested)
        {
            return;
        }

        _until = clock.GetUtcNow().AddSeconds(seconds);
        try
        {
            await js.InvokeVoidAsync("sessionStorage.setItem", storageKey, _until.Value.ToUnixTimeMilliseconds().ToString());
        }
        finally
        {
            if (!_dispose.IsCancellationRequested) _ = UpdateAsync();
        }
    }

    private async Task UpdateAsync()
    {
        try
        {
            while (RemainingSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), clock, _dispose.Token);
                await refresh();
            }
        }
        catch (OperationCanceledException) when (_dispose.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        _dispose.Cancel();
        _dispose.Dispose();
    }
}
