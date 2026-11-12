namespace BookStore.Identity.Common;

internal static class IdentityTransactionLock
{
    internal static async Task AcquireAsync(IdentityDbContext context, string resource, CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @result < 0 THROW 51000, 'Could not acquire identity lock.', 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static string User(string userId) =>
        "user:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));
}
