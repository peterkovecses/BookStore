namespace BookStore.UI.UnitTests.Common.Services;

public class NotificationServiceTests
{
    private readonly NotificationService _sut = new();

    [Fact]
    public void ShowSuccess_ShouldRaiseOnNotificationAddedWithCorrectData()
    {
        // Arrange
        const string expectedMessage = "Success message";
        Notification? actual = null;
        _sut.OnNotificationAdded += n => actual = n;

        // Act
        _sut.ShowSuccess(expectedMessage);

        // Assert
        actual.Should().NotBeNull();
        actual.Message.Should().Be(expectedMessage);
        actual.IsSuccess.Should().BeTrue();
        actual.AutoHide.Should().BeTrue();
        actual.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ShowFailure_ShouldRaiseOnNotificationAddedWithCorrectData()
    {
        // Arrange
        const string expectedMessage = "Error message";
        Notification? actual = null;
        _sut.OnNotificationAdded += n => actual = n;

        // Act
        _sut.ShowFailure(expectedMessage);

        // Assert
        actual.Should().NotBeNull();
        actual.Message.Should().Be(expectedMessage);
        actual.IsSuccess.Should().BeFalse();
        actual.AutoHide.Should().BeFalse();
        actual.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ShowFailure_WhenNoMessagePassed_ShouldUseDefaultMessage()
    {
        // Arrange
        Notification? actual = null;
        _sut.OnNotificationAdded += n => actual = n;

        // Act
        _sut.ShowFailure();

        // Assert
        actual.Should().NotBeNull();
        actual.Message.Should().Be("Something went wrong, please try again later.");
    }

    [Fact]
    public void ClearNotifications_ShouldRaiseOnNotificationsCleared()
    {
        // Arrange
        var raised = false;
        _sut.OnNotificationsCleared += () => raised = true;

        // Act
        _sut.ClearNotifications();

        // Assert
        raised.Should().BeTrue();
    }


    [Fact]
    public void ShowSuccess_WhenNoSubscriber_ShouldNotThrow()
    {
        // Arrange & Act
        var act = () => _sut.ShowSuccess("Message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ClearNotifications_WhenNoSubscriber_ShouldNotThrow()
    {
        // Arrange & Act
        var act = () => _sut.ClearNotifications();

        // Assert
        act.Should().NotThrow();
    }
}