namespace BookStore.UI.Features.Shared.BaseComponents;

/// <summary>
/// Base class for all custom components in the BookStore application.
/// </summary>
public abstract class CustomComponentBase : ComponentBase, IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value indicating whether the component is currently loading data.
    /// </summary>
    protected bool Loading { get; set; }

    /// <summary>
    /// Gets a cancellation token that is cancelled when the component is disposed.
    /// </summary>
    protected CancellationToken CancellationToken 
        => (_cancellationTokenSource ??= new CancellationTokenSource()).Token;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public virtual void Dispose()
    {
        if (_cancellationTokenSource is not null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        GC.SuppressFinalize(this);
    }
}
