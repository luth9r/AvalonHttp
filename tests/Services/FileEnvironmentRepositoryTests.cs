using System;
using System.Threading.Tasks;
using AvalonHttp.Services;
using AvalonHttp.Services.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace AvalonHttpTests.Services;

public class FileEnvironmentRepositoryTests : IAsyncLifetime
{
    private readonly FileEnvironmentRepository _sut;
    private readonly Guid _testEnvId;

    public FileEnvironmentRepositoryTests()
    {
        var sanitizerMock = new Mock<IFileNameSanitizer>();
        sanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>())).Returns((string s) => s.Replace(" ", "_"));
        
        _sut = new FileEnvironmentRepository(sanitizerMock.Object);
        _testEnvId = Guid.NewGuid();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup after tests
        await _sut.DeleteAsync(_testEnvId);
    }

    [Fact]
    public async Task SaveAndLoadAll_WorksCorrectly()
    {
        // Arrange
        var env = new AvalonHttp.Models.EnvironmentAggregate.Environment
        {
            Id = _testEnvId,
            Name = "Test Environment",
            VariablesJson = "{}"
        };

        // Act
        await _sut.SaveAsync(env);
        var all = await _sut.LoadAllAsync();

        // Assert
        all.Should().Contain(e => e.Id == _testEnvId && e.Name == "Test Environment");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEnvironment()
    {
        // Arrange
        var env = new AvalonHttp.Models.EnvironmentAggregate.Environment
        {
            Id = _testEnvId,
            Name = "To Be Deleted",
            VariablesJson = "{}"
        };
        await _sut.SaveAsync(env);

        // Act
        await _sut.DeleteAsync(_testEnvId);
        var all = await _sut.LoadAllAsync();

        // Assert
        all.Should().NotContain(e => e.Id == _testEnvId);
    }

    [Fact]
    public async Task SetAndGetActiveEnvironment_WorksCorrectly()
    {
        // Arrange
        var env = new AvalonHttp.Models.EnvironmentAggregate.Environment
        {
            Id = _testEnvId,
            Name = "Active Environment",
            VariablesJson = "{}"
        };
        await _sut.SaveAsync(env);

        try
        {
            // Act
            await _sut.SetActiveEnvironmentAsync(_testEnvId);
            var active = await _sut.GetActiveEnvironmentAsync();

            // Assert
            active.Should().NotBeNull();
            active!.Id.Should().Be(_testEnvId);
        }
        finally
        {
            // Reset active environment
            await _sut.SetActiveEnvironmentAsync(null);
        }
    }
}
