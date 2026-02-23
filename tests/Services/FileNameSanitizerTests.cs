using System.IO;
using AvalonHttp.Services;
using FluentAssertions;
using Xunit;

namespace AvalonHttpTests.Services;

public class FileNameSanitizerTests
{
    private readonly FileNameSanitizer _sut;

    public FileNameSanitizerTests()
    {
        _sut = new FileNameSanitizer();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Sanitize_WithNullOrWhiteSpace_ReturnsUnnamed(string? input)
    {
        // Act
        var result = _sut.Sanitize(input);

        // Assert
        result.Should().Be("Unnamed");
    }

    [Fact]
    public void Sanitize_WithValidFileName_ReturnsOriginal()
    {
        // Arrange
        var input = "valid_file_name.txt";

        // Act
        var result = _sut.Sanitize(input);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void Sanitize_WithInvalidChars_ReplacesWithUnderscore()
    {
        // Arrange
        var input = "invalid<file>name:*.txt";

        // Act
        var result = _sut.Sanitize(input);

        // Assert
        result.Should().NotContainAny(Path.GetInvalidFileNameChars().Select(c => c.ToString()));
        result.Should().Be("invalid_file_name__.txt");
    }

    [Fact]
    public void Sanitize_WithLongFileName_TruncatesTo50Chars()
    {
        // Arrange
        var input = new string('a', 60);

        // Act
        var result = _sut.Sanitize(input);

        // Assert
        result.Length.Should().Be(50);
        result.Should().Be(new string('a', 50));
    }
}
