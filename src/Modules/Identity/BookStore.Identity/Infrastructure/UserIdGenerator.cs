namespace BookStore.Identity.Infrastructure;

internal interface IUserIdGenerator
{
    string Create();
}

internal sealed class UserIdGenerator : IUserIdGenerator
{
    public string Create() => Guid.NewGuid().ToString();
}
