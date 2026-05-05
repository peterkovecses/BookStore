namespace BookStore.Testing.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and related types within the BookStore domain.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Asserts that the result is a success and returns specialized assertions for the <see cref="PaginatedList{T}"/> data.
    /// </summary>
    /// <typeparam name="T">The type of items in the paginated list.</typeparam>
    /// <param name="result">The result instance to assert on.</param>
    /// <returns>A <see cref="PaginatedListAssertions{T}"/> instance for further assertions.</returns>
    public static PaginatedListAssertions<T> ShouldBePaginatedSuccess<T>(this Result<PaginatedList<T>> result)
    {
        result.Should().BeSuccess();
        result.Data.Should().NotBeNull();
        
        return new PaginatedListAssertions<T>(result.Data!);
    }
}

/// <summary>
/// Provides specialized fluent assertions for <see cref="PaginatedList{T}"/>.
/// </summary>
/// <typeparam name="T">The type of items in the paginated list.</typeparam>
/// <param name="subject">The paginated list instance being asserted.</param>
public class PaginatedListAssertions<T>(PaginatedList<T> subject)
{
    /// <summary>
    /// Gets the paginated list instance being asserted.
    /// </summary>
    public PaginatedList<T> Subject { get; } = subject;

    /// <summary>
    /// Asserts that the number of items in the paginated list matches the expected count.
    /// </summary>
    /// <param name="expected">The expected number of items.</param>
    /// <returns>The current <see cref="PaginatedListAssertions{T}"/> instance for further assertions.</returns>
    public PaginatedListAssertions<T> HaveCount(int expected)
    {
        Subject.Items.Count.Should().Be(expected);
        
        return this;
    }

    /// <summary>
    /// Asserts that the page number of the paginated list matches the expected value.
    /// </summary>
    /// <param name="expected">The expected page number.</param>
    /// <returns>The current <see cref="PaginatedListAssertions{T}"/> instance for further assertions.</returns>
    public PaginatedListAssertions<T> HavePageNumber(int expected)
    {
        Subject.PageNumber.Should().Be(expected);
        
        return this;
    }

    /// <summary>
    /// Asserts that the total count of items in the source matches the expected value.
    /// </summary>
    /// <param name="expected">The expected total count.</param>
    /// <returns>The current <see cref="PaginatedListAssertions{T}"/> instance for further assertions.</returns>
    public PaginatedListAssertions<T> HaveTotalCount(int expected)
    {
        Subject.TotalCount.Should().Be(expected);
        
        return this;
    }

    /// <summary>
    /// Asserts that all items in the paginated list satisfy the specified inspection action.
    /// </summary>
    /// <param name="inspection">The action to perform on each item.</param>
    /// <returns>The current <see cref="PaginatedListAssertions{T}"/> instance for further assertions.</returns>
    public PaginatedListAssertions<T> AllSatisfy(Action<T> inspection)
    {
        Subject.Items.Should().AllSatisfy(inspection);
        
        return this;
    }

    /// <summary>
    /// Asserts that the items in the paginated list satisfy the specified sequence of inspection actions.
    /// </summary>
    /// <param name="inspections">The actions to perform on each item in order.</param>
    /// <returns>The current <see cref="PaginatedListAssertions{T}"/> instance for further assertions.</returns>
    public PaginatedListAssertions<T> SatisfyRespectively(params Action<T>[] inspections)
    {
        Subject.Items.Should().SatisfyRespectively(inspections);
        
        return this;
    }

    /// <summary>
    /// Asserts that the paginated list is equivalent to the specified expected object.
    /// </summary>
    /// <param name="expected">The object to compare with.</param>
    public void BeEquivalentTo(object expected) => Subject.Should().BeEquivalentTo(expected);
}
