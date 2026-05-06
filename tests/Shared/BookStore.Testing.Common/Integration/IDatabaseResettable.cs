namespace BookStore.Testing.Common.Integration;

/// <summary>
/// Interface for factories that support database resetting between tests.
/// </summary>
public interface IDatabaseResettable
{
    /// <summary>
    /// Resets the database to its initial state.
    /// </summary>
    Task ResetDatabaseAsync();
}
