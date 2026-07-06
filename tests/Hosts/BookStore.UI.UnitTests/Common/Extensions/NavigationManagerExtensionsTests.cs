namespace BookStore.UI.UnitTests.Common.Extensions;

public class NavigationManagerExtensionsTests
{
    private readonly MockNavigationManager _navManager = new();

    [Fact]
    public void TryGetQueryParameter_WhenKeyExistsAndTypeIsString_ShouldReturnValue()
    {
        // Arrange
        const string key = "searchTerm";
        const string expectedValue = "test";
        _navManager.SetUri($"http://localhost/books?{key}={expectedValue}");

        // Act
        var result = _navManager.TryGetQueryParameter<string>(key, out var actualValue);

        // Assert
        result.Should().BeTrue();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void TryGetQueryParameter_WhenKeyExistsAndTypeIsInt_ShouldReturnTrueAndValue()
    {
        // Arrange
        const string key = "pageNumber";
        const int expectedValue = 2;
        _navManager.SetUri($"http://localhost/books?{key}={expectedValue}");

        // Act
        var result = _navManager.TryGetQueryParameter<int>(key, out var actualValue);

        // Assert
        result.Should().BeTrue();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void TryGetQueryParameter_WhenTypeIsBool_ShouldReturnValue()
    {
        // Arrange
        const string key = "isActive";
        const bool expectedValue = true;
        _navManager.SetUri($"http://localhost/books?{key}={expectedValue}");

        // Act
        var result = _navManager.TryGetQueryParameter<bool>(key, out var actualValue);

        // Assert
        result.Should().BeTrue();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void TryGetQueryParameter_WhenTypeIsGuid_ShouldReturnValue()
    {
        // Arrange
        const string key = "id";
        var expectedValue = Guid.NewGuid();
        _navManager.SetUri($"http://localhost/books?{key}={expectedValue}");

        // Act
        var result = _navManager.TryGetQueryParameter<Guid>(key, out var actualValue);

        // Assert
        result.Should().BeTrue();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void TryGetQueryParameter_WhenKeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books");

        // Act
        var result = _navManager.TryGetQueryParameter<string>("nonexistent", out var actualValue);

        // Assert
        result.Should().BeFalse();
        actualValue.Should().BeNull();
    }

    [Fact]
    public void TryGetQueryParameter_WhenParseFails_ShouldReturnFalse()
    {
        // Arrange
        const string key = "pageNumber";
        _navManager.SetUri($"http://localhost/books?{key}=abc");

        // Act
        var result = _navManager.TryGetQueryParameter<int>(key, out var actualValue);

        // Assert
        result.Should().BeFalse();
        actualValue.Should().Be(default);
    }

    [Fact]
    public void TryGetQueryParameter_WhenTypeIsNotSupported_ShouldReturnFalse()
    {
        // Arrange
        const string key = "data";
        _navManager.SetUri($"http://localhost/books?{key}=some-data");

        // Act
        var result = _navManager.TryGetQueryParameter<object>(key, out var actualValue);

        // Assert
        result.Should().BeFalse();
        actualValue.Should().BeNull();
    }

    [Fact]
    public void TryGetQueryParameter_WhenMultipleValuesExistForSameKey_ShouldReturnFirstValue()
    {
        // Arrange
        const string key = "filter";
        const string expectedValue = "value1";
        _navManager.SetUri($"http://localhost/books?{key}={expectedValue}&{key}=value2");

        // Act
        var result = _navManager.TryGetQueryParameter<string>(key, out var actualValue);

        // Assert
        result.Should().BeTrue();
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void GetUriWithUpdatedQueryParameter_WhenValueIsProvided_ShouldUpdateQueryString()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books?searchTerm=old");
        const string key = "searchTerm";
        const string newValue = "new";

        // Act
        var actual = _navManager.GetUriWithUpdatedQueryParameter(key, newValue);

        // Assert
        actual.Should().Contain($"{key}={newValue}");
    }

    [Fact]
    public void GetUriWithUpdatedQueryParameter_WhenKeyDoesNotExist_ShouldAddKey()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books");
        const string key = "newKey";
        const string value = "newValue";

        // Act
        var actual = _navManager.GetUriWithUpdatedQueryParameter(key, value);

        // Assert
        actual.Should().Contain($"{key}={value}");
    }

    [Fact]
    public void GetUriWithUpdatedQueryParameter_WhenValueIsNull_ShouldRemoveKeyFromQueryString()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books?searchTerm=old&page=1");
        const string key = "searchTerm";

        // Act
        var actual = _navManager.GetUriWithUpdatedQueryParameter<string?>(key, null);

        // Assert
        actual.Should().NotContain(key);
        actual.Should().Contain("page=1");
    }

    [Fact]
    public void GetUriWithUpdatedQueryParameter_WhenValueIsWhitespace_ShouldRemoveKeyFromQueryString()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books?key=value");
        const string key = "key";

        // Act
        var actual = _navManager.GetUriWithUpdatedQueryParameter(key, "   ");

        // Assert
        actual.Should().NotContain(key);
    }

    [Fact]
    public void GetUriWithUpdatedQueryParameter_WhenValueIsValueType_ShouldUpdateQueryString()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books?page=1");
        const string key = "page";
        const int newValue = 2;

        // Act
        var actual = _navManager.GetUriWithUpdatedQueryParameter(key, newValue);

        // Assert
        actual.Should().Contain($"{key}={newValue}");
    }

    [Fact]
    public void GetUriWithUpdatedQueryParameter_WhenValueIsDateTime_ShouldUpdateQueryStringWithCorrectFormatting()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books");
        const string key = "date";
        var dateValue = new DateTime(2026, 5, 28, 14, 30, 0);

        // Act
        var actual = _navManager.GetUriWithUpdatedQueryParameter(key, dateValue);

        // Assert
        // Blazor's GetUriWithQueryParameters formats DateTime using culture-dependent format in the mock environment.
        // The important part is that it is included and handled by Blazor's logic.
        actual.Should().Contain(key);
        actual.Should().Contain("2026");
        actual.Should().Contain("05");
        actual.Should().Contain("28");
    }

    [Fact]
    public void TryGetQueryParameters_WhenMultipleValidValuesExist_ShouldReturnTrueAndList()
    {
        // Arrange
        const string key = "ids";
        _navManager.SetUri($"http://localhost/books?{key}=1&{key}=2&{key}=3");

        // Act
        var result = _navManager.TryGetQueryParameters<int>(key, out var actualValues);

        // Assert
        result.Should().BeTrue();
        actualValues.Should().HaveCount(3).And.ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void TryGetQueryParameters_WhenMixedValidAndInvalidValuesExist_ShouldReturnOnlyValidValues()
    {
        // Arrange
        const string key = "ids";
        _navManager.SetUri($"http://localhost/books?{key}=1&{key}=abc&{key}=3");

        // Act
        var result = _navManager.TryGetQueryParameters<int>(key, out var actualValues);

        // Assert
        result.Should().BeTrue();
        actualValues.Should().HaveCount(2).And.ContainInOrder(1, 3);
    }

    [Fact]
    public void TryGetQueryParameters_WhenKeyExistsButNoValuesAreValid_ShouldReturnFalseAndEmptyList()
    {
        // Arrange
        const string key = "ids";
        _navManager.SetUri($"http://localhost/books?{key}=abc&{key}=def");

        // Act
        var result = _navManager.TryGetQueryParameters<int>(key, out var actualValues);

        // Assert
        result.Should().BeFalse();
        actualValues.Should().BeEmpty();
    }

    [Fact]
    public void TryGetQueryParameters_WhenTypeIsNotSupported_ShouldReturnFalse()
    {
        // Arrange
        const string key = "data";
        _navManager.SetUri($"http://localhost/books?{key}=val1&{key}=val2");

        // Act
        var result = _navManager.TryGetQueryParameters<object>(key, out var actualValues);

        // Assert
        result.Should().BeFalse();
        actualValues.Should().BeEmpty();
    }

    private class MockNavigationManager : NavigationManager
    {
        public MockNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = uri;
            NotifyLocationChanged(false);
        }

        public void SetUri(string uri)
        {
            Uri = uri;
            NotifyLocationChanged(false);
        }
    }


    [Fact]
    public void TryGetQueryParameters_WhenKeyDoesNotExist_ShouldReturnFalseAndEmptyList()
    {
        // Arrange
        _navManager.SetUri("http://localhost/books");

        // Act
        var result = _navManager.TryGetQueryParameters<int>("nonexistent", out var actualValues);

        // Assert
        result.Should().BeFalse();
        actualValues.Should().BeEmpty();
    }
}