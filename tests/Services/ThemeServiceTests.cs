using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services;
using AvalonHttp.Services.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace AvalonHttpTests.Services;

public class ThemeServiceTests
{
    private readonly Mock<ISessionService> _sessionServiceMock;
    private readonly Mock<IThemeApplicator> _themeApplicatorMock;
    private readonly ThemeService _sut;

    public ThemeServiceTests()
    {
        _sessionServiceMock = new Mock<ISessionService>();
        _themeApplicatorMock = new Mock<IThemeApplicator>();
        _sut = new ThemeService(_sessionServiceMock.Object, _themeApplicatorMock.Object);
    }

    [Fact]
    public void Constructor_Initialization_DefaultThemeIsDark()
    {
        // Assert
        _sut.CurrentTheme.Should().Be("Dark");
    }

    [Fact]
    public void Init_WithNoSavedTheme_DefaultsToDark()
    {
        // Arrange
        _sessionServiceMock.Setup(x => x.LoadState()).Returns(new AppState { Theme = null });

        // Act
        _sut.Init();

        // Assert
        _sut.CurrentTheme.Should().Be("Dark");
    }

    [Fact]
    public void Init_WithInvalidSavedTheme_DefaultsToDark()
    {
        // Arrange
        _sessionServiceMock.Setup(x => x.LoadState()).Returns(new AppState { Theme = "Blue" });

        // Act
        _sut.Init();

        // Assert
        _sut.CurrentTheme.Should().Be("Dark");
    }

    [Fact]
    public void Init_WithValidSavedTheme_SetsTheme()
    {
        // Arrange
        _sessionServiceMock.Setup(x => x.LoadState()).Returns(new AppState { Theme = "Light" });

        // Act
        _sut.Init();

        // Assert
        _sut.CurrentTheme.Should().Be("Light");
    }

    [Fact]
    public async Task ChangeThemeAsync_WithInvalidTheme_DoesNothing()
    {
        // Act
        await _sut.ChangeThemeAsync("Blue");

        // Assert
        _sut.CurrentTheme.Should().Be("Dark"); // default
        _sessionServiceMock.Verify(x => x.SaveThemeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangeThemeAsync_WithValidTheme_UpdatesThemeAndSaves()
    {
        // Act
        await _sut.ChangeThemeAsync("Light");

        // Assert
        _sut.CurrentTheme.Should().Be("Light");
        _sessionServiceMock.Verify(x => x.SaveThemeAsync("Light"), Times.Once);
    }

    [Fact]
    public async Task ChangeThemeAsync_WithSameTheme_DoesNothing()
    {
        // Act
        await _sut.ChangeThemeAsync("Dark"); // assuming it starts with Dark

        // Assert
        _sut.CurrentTheme.Should().Be("Dark");
        _sessionServiceMock.Verify(x => x.SaveThemeAsync(It.IsAny<string>()), Times.Never);
    }
}
