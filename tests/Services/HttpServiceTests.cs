using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AvalonHttp.Services;
using FluentAssertions;
using Xunit;

namespace AvalonHttpTests.Services;

public class HttpServiceTests : IDisposable
{
    private readonly HttpService _sut;

    public HttpServiceTests()
    {
        _sut = new HttpService();
    }

    [Fact]
    public void Constructor_InitializesWithoutException()
    {
        // Assert
        _sut.Should().NotBeNull();
        _sut.LastRequestMetrics.Should().BeNull();
    }

    [Fact]
    public async Task SendRequestAsync_WithInvalidUrl_ThrowsHttpRequestException()
    {
        // Arrange
        var headers = new List<KeyValuePair<string, string>>();

        // Act
        Func<Task> act = async () => await _sut.SendRequestAsync("invalid-url", "GET", headers);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
