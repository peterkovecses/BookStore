namespace BookStore.Catalog.UnitTests.Infrastructure.Storage;

public class AzureCatalogStorageServiceTests
{
    private readonly BlobServiceClient _blobServiceClient = Substitute.For<BlobServiceClient>();
    private readonly AzureCatalogStorageService _sut;

    public AzureCatalogStorageServiceTests()
    {
        _sut = new AzureCatalogStorageService(_blobServiceClient);
    }

    [Fact]
    public void GetPublicUrl_WhenImageNameIsValid_ShouldReturnUrl()
    {
        // Arrange
        var imageName = "test-image.jpg";
        var expectedUrl = "https://bookstore.blob.core.windows.net/book-covers/test-image.jpg";
        var containerClient = Substitute.For<BlobContainerClient>();
        var blobClient = Substitute.For<BlobClient>();

        _blobServiceClient.GetBlobContainerClient("book-covers").Returns(containerClient);
        containerClient.GetBlobClient(imageName).Returns(blobClient);
        blobClient.Uri.Returns(new Uri(expectedUrl));

        // Act
        var actual = _sut.GetPublicUrl(imageName);

        // Assert
        actual.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetPublicUrl_WhenImageNameIsNullOrWhiteSpace_ShouldThrowArgumentException(string? imageName)
    {
        // Act
        var act = () => _sut.GetPublicUrl(imageName!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
