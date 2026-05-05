namespace BookStore.Common.UnitTests.Sorting;

public class SortingExtensionsTests
{
    private sealed record TestEntity(int Id, string Name);

    [Fact]
    public void ApplySorting_WhenSortByIsMissing_ShouldReturnOriginalQueryable()
    {
        // Arrange
        var data = new List<TestEntity> { new(1, "B"), new(3, "C"),  new(2, "A") }.AsQueryable();
        var sorting = Substitute.For<ISorting>();
        sorting.SortBy.Returns((string?)null);

        // Act
        var actual = data.ApplySorting(sorting);

        // Assert
        actual.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void ApplySorting_WhenSortByIsPresent_ShouldSortAscending()
    {
        // Arrange
        var data = new List<TestEntity> 
        { 
            new(2, "B"), 
            new(1, "A"), 
            new(3, "C") 
        }.AsQueryable();
        
        var sorting = Substitute.For<ISorting>();
        sorting.SortBy.Returns("Id");
        sorting.IsDescending.Returns(false);

        // Act
        var actual = data.ApplySorting(sorting).ToList();

        // Assert
        actual.Select(entity => entity.Id).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ApplySorting_WhenIsDescendingIsTrue_ShouldSortDescending()
    {
        // Arrange
        var data = new List<TestEntity> 
        { 
            new(2, "B"), 
            new(1, "A"), 
            new(3, "C") 
        }.AsQueryable();
        
        var sorting = Substitute.For<ISorting>();
        sorting.SortBy.Returns("Name");
        sorting.IsDescending.Returns(true);

        // Act
        var actual = data.ApplySorting(sorting).ToList();

        // Assert
        actual.Select(entity => entity.Name).Should().BeInDescendingOrder();
    }

    [Fact]
    public void ApplySorting_WhenPropertyNotFound_ShouldThrowArgumentException()
    {
        // Arrange
        var data = new List<TestEntity> { new(1, "A") }.AsQueryable();
        var sorting = Substitute.For<ISorting>();
        sorting.SortBy.Returns("NonExistent");

        // Act
        Action act = () => data.ApplySorting(sorting);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
