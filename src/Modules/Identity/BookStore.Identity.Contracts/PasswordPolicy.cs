namespace BookStore.Identity.Contracts;

public sealed class PasswordPolicy
{
    public required int SchemaVersion { get; init; }
    public required int RequiredLength { get; init; }
    public required int RequiredUniqueChars { get; init; }
    public required bool RequireDigit { get; init; }
    public required bool RequireLowercase { get; init; }
    public required bool RequireUppercase { get; init; }
    public required bool RequireNonAlphanumeric { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequiredLength >= 6 && RequiredUniqueChars >= 1;
}
