using System;
using System.Threading.Tasks;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services;
using AvalonHttp.Services.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace AvalonHttpTests.Services;

public class FileCollectionRepositoryTests : IAsyncLifetime
{
    private readonly FileCollectionRepository _sut;
    private readonly Guid _testCollectionId;

    public FileCollectionRepositoryTests()
    {
        var sanitizerMock = new Mock<IFileNameSanitizer>();
        sanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>())).Returns((string s) => s.Replace(" ", "_"));
        
        _sut = new FileCollectionRepository(sanitizerMock.Object);
        _testCollectionId = Guid.NewGuid();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup after tests
        await _sut.DeleteAsync(_testCollectionId);
    }

    [Fact]
    public async Task SaveAndGetById_WorksCorrectly()
    {
        // Arrange
        var collection = new ApiCollection
        {
            Id = _testCollectionId,
            Name = "Test Collection"
        };

        // Act
        await _sut.SaveAsync(collection);
        var loaded = await _sut.GetByIdAsync(_testCollectionId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(_testCollectionId);
        loaded.Name.Should().Be("Test Collection");
    }

    [Fact]
    public async Task DeleteAsync_RemovesCollection()
    {
        // Arrange
        var collection = new ApiCollection
        {
            Id = _testCollectionId,
            Name = "To Be Deleted"
        };
        await _sut.SaveAsync(collection);

        // Act
        await _sut.DeleteAsync(_testCollectionId);
        var loaded = await _sut.GetByIdAsync(_testCollectionId);

        // Assert
        loaded.Should().BeNull();
    }
}
