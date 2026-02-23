using System;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services;
using AvalonHttp.Services.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace AvalonHttpTests.Services;

public class SessionServiceTests
{
    private readonly SessionService _sut;
    private readonly Mock<IFileStorage<AppState>> _storageMock;

    public SessionServiceTests()
    {
        _storageMock = new  Mock<IFileStorage<AppState>>();
        
        _sut = new SessionService(_storageMock.Object);
    }

    [Fact]
    public void LoadState_ReturnsValidState()
    {
        // Arrange
        var expectedState = new AppState { Theme = "Light" };
        _storageMock.Setup(x => x.Load()).Returns(expectedState);

        // Act
        var state = _sut.LoadState();

        // Assert
        state.Should().NotBeNull();
        state.Theme.Should().Be("Light");
        
        _storageMock.Verify(x => x.Load(), Times.Once);
    }

    [Fact]
    public async Task SaveLanguageAsync_UpdatesStateAndCanBeLoaded()
    {
        var testState = new AppState { Language = "en" };

        _storageMock.Setup(x => x.UpdateAsync(It.IsAny<Action<AppState>>()))
            .Callback<Action<AppState>>(action => action(testState))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SaveLanguageAsync("ua");

        // Assert
        testState.Language.Should().Be("ua");
        _storageMock.Verify(x => x.UpdateAsync(It.IsAny<Action<AppState>>()), Times.Once);
    }
}
