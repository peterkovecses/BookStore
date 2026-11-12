namespace BookStore.Identity.Common;

internal static class IdentityTransactionLock
{
    internal static Task AcquireAsync(IIdentityDbTransaction transaction, string resource, CancellationToken cancellationToken) =>
        transaction.AcquireLockAsync(resource, cancellationToken);

    internal static string User(string userId) =>
        "user:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));
}
