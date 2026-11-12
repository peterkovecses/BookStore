namespace BookStore.UI.Features.Authentication;

internal interface ISessionLock
{
    Task AcquireAsync();
    Task ReleaseAsync();
}

internal sealed class BrowserSessionLock(IJSRuntime js) : ISessionLock
{
    public async Task AcquireAsync() => await js.InvokeVoidAsync("bookStoreSessionLock.acquire");

    public async Task ReleaseAsync() => await js.InvokeVoidAsync("bookStoreSessionLock.release");
}
