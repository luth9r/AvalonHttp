using System.Collections.Generic;
using AvalonHttp.Models;
using AvalonHttp.Services;
using FluentAssertions;
using Xunit;

namespace AvalonHttpTests.Services;

public class UrlParserServiceTests
{
    private readonly UrlParserService _sut;

    public UrlParserServiceTests()
    {
        _sut = new UrlParserService();
    }

    [Fact]
    public void ParseUrl_WithNullUrl_ReturnsEmptyStringAndNoParams()
    {
        // Act
        var (baseUrl, parameters) = _sut.ParseUrl(null!);

        // Assert
        baseUrl.Should().BeEmpty();
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void ParseUrl_WithAbsoluteUrl_ReturnsBaseUrlAndParams()
    {
        // Arrange
        var url = "http://example.com/api/users?id=1&name=test";

        // Act
        var (baseUrl, parameters) = _sut.ParseUrl(url);

        // Assert
        baseUrl.Should().Be("http://example.com/api/users");
        parameters.Should().HaveCount(2);
        parameters[0].Key.Should().Be("id");
        parameters[0].Value.Should().Be("1");
        parameters[1].Key.Should().Be("name");
        parameters[1].Value.Should().Be("test");
    }

    [Fact]
    public void ParseUrl_WithFragment_RemovesFragment()
    {
        // Arrange
        var url = "http://example.com/api/#section?id=1";

        // Act
        var (baseUrl, parameters) = _sut.ParseUrl(url);

        // Assert
        baseUrl.Should().Be("http://example.com/api/");
        parameters.Should().BeEmpty(); // Since # is removed before processing query
    }

    [Fact]
    public void BuildUrl_WithNullBaseUrl_ReturnsEmptyString()
    {
        // Act
        var result = _sut.BuildUrl(null!, new List<KeyValueItemModel>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildUrl_WithDisabledParams_IgnoresDisabledParams()
    {
        // Arrange
        var baseUrl = "http://example.com/api";
        var parameters = new List<KeyValueItemModel>
        {
            new() { Key = "id", Value = "1", IsEnabled = true },
            new() { Key = "name", Value = "test", IsEnabled = false }
        };

        // Act
        var result = _sut.BuildUrl(baseUrl, parameters);

        // Assert
        result.Should().Be("http://example.com/api?id=1");
    }

    [Fact]
    public void BuildUrl_WithBaseUrlContainingQuery_AppendsWithAmpersand()
    {
        // Arrange
        var baseUrl = "http://example.com/api?existing=yes";
        var parameters = new List<KeyValueItemModel>
        {
            new() { Key = "id", Value = "1", IsEnabled = true }
        };

        // Act
        var result = _sut.BuildUrl(baseUrl, parameters);

        // Assert
        result.Should().Be("http://example.com/api?existing=yes&id=1");
    }
}
