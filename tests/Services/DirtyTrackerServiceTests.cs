using AvalonHttp.Services;
using FluentAssertions;
using Xunit;

namespace AvalonHttpTests.Services;

public class DirtyTrackerServiceTests
{
    private class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private readonly DirtyTrackerService _sut;

    public DirtyTrackerServiceTests()
    {
        _sut = new DirtyTrackerService();
    }

    [Fact]
    public void TakeSnapshot_CreatesValidJson()
    {
        // Arrange
        var obj = new TestModel { Id = 1, Name = "Test" };

        // Act
        var snapshot = _sut.TakeSnapshot(obj);

        // Assert
        snapshot.Should().Contain("\"Id\":1");
        snapshot.Should().Contain("\"Name\":\"Test\"");
    }

    [Fact]
    public void IsDirty_WithNullOrEmptySnapshot_ReturnsFalse()
    {
        // Arrange
        var obj = new TestModel { Id = 1, Name = "Test" };

        // Act
        var resultNull = _sut.IsDirty(obj, null!);
        var resultEmpty = _sut.IsDirty(obj, string.Empty);

        // Assert
        resultNull.Should().BeFalse();
        resultEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsDirty_WithSameObject_ReturnsFalse()
    {
        // Arrange
        var obj = new TestModel { Id = 1, Name = "Test" };
        var snapshot = _sut.TakeSnapshot(obj);

        // Act
        var result = _sut.IsDirty(obj, snapshot);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDirty_WithModifiedObject_ReturnsTrue()
    {
        // Arrange
        var obj = new TestModel { Id = 1, Name = "Test" };
        var snapshot = _sut.TakeSnapshot(obj);

        // Act
        obj.Name = "Modified";
        var result = _sut.IsDirty(obj, snapshot);

        // Assert
        result.Should().BeTrue();
    }
}
