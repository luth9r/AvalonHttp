using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using AvalonHttp.Services;
using AvalonHttpTests.Helpers;
using FluentAssertions;
using Xunit;

namespace AvalonHttpTests.Services;

public class HttpServiceTests : IDisposable
{
    private static HttpService CreateSut(HttpMessageHandler fakeHandler)
        => new(fakeHandler);

    private static List<KeyValuePair<string, string>> NoHeaders()
        => new();

    private static List<KeyValuePair<string, string>> Headers(params (string k, string v)[] pairs)
    {
        var list = new List<KeyValuePair<string, string>>();
        foreach (var (k, v) in pairs)
        {
            list.Add(new(k, v));
        }

        return list;
    }

    private readonly HttpService _defaultSut;

    public HttpServiceTests()
    {
        _defaultSut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));
    }

    [Fact]
    public void Constructor_InitializesWithoutException()
    {
        _defaultSut.Should().NotBeNull();
        _defaultSut.LastRequestMetrics.Should().BeNull();
    }

    [Fact]
    public async Task SendRequestAsync_Get_ReturnsSuccessResponse()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{\"id\":1}");
        using var sut = CreateSut(fake);

        var response = await sut.SendRequestAsync(
            "http://test.local/api", "GET", NoHeaders());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"id\":1");
    }

    [Fact]
    public async Task SendRequestAsync_Get_PopulatesLastRequestMetrics()
    {
        using var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));

        await sut.SendRequestAsync("http://test.local/api", "GET", NoHeaders());

        sut.LastRequestMetrics.Should().NotBeNull();
    }

    [Fact]
    public async Task SendRequestAsync_Get_MetricsTimeToFirstByte_NonNegative()
    {
        using var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));

        await sut.SendRequestAsync("http://test.local/api", "GET", NoHeaders());

        sut.LastRequestMetrics!.TimeToFirstByte.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SendRequestAsync_Get_MetricsContentDownload_NonNegative()
    {
        using var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));

        await sut.SendRequestAsync("http://test.local/api", "GET", NoHeaders());

        sut.LastRequestMetrics!.ContentDownload.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SendRequestAsync_VariousMethods_SendsCorrectHttpMethod(string method)
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://test.local/api", method, NoHeaders());

        fake.LastRequest!.Method.Method.Should().Be(method);
    }

    [Fact]
    public async Task SendRequestAsync_MethodIsCasePreserved()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com", "get", NoHeaders());

        fake.LastRequest!.Method.Method.Should().Be("get");
    }

    [Fact]
    public async Task SendRequestAsync_HttpUrl_SendsToCorrectHost()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com/v1/users", "GET", NoHeaders());

        fake.LastRequest!.RequestUri!.Host.Should().Be("api.example.com");
    }

    [Fact]
    public async Task SendRequestAsync_WithPath_SendsToCorrectPath()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com/v1/users/42", "GET", NoHeaders());

        fake.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/v1/users/42");
    }

    [Fact]
    public async Task SendRequestAsync_HttpsUrl_PreservesScheme()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("https://secure.api.com/data", "GET", NoHeaders());

        fake.LastRequest!.RequestUri!.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task SendRequestAsync_UrlWithQueryString_PreservesAllParams()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync(
            "http://api.example.com/search?q=hello&page=2&limit=10",
            "GET", NoHeaders());

        var query = HttpUtility.ParseQueryString(fake.LastRequest!.RequestUri!.Query);

        query["q"].Should().Be("hello");
        query["page"].Should().Be("2");
        query["limit"].Should().Be("10");
    }

    [Fact]
    public async Task SendRequestAsync_UrlWithSpecialCharsInQuery_PreservesEncoding()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        var url = "http://api.example.com/search?q=hello%20world&filter=a%2Bb";

        await sut.SendRequestAsync(url, "GET", NoHeaders());

        fake.LastRequest!.RequestUri!.Query.Should().Contain("q=hello%20world");
    }

    [Fact]
    public async Task SendRequestAsync_UrlWithoutQueryString_HasEmptyQuery()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com/items", "GET", NoHeaders());

        fake.LastRequest!.RequestUri!.Query.Should().BeEmpty();
    }

    [Fact]
    public async Task SendRequestAsync_UrlWithFragment_IsPreserved()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);
        var url = "http://api.example.com/page?id=5";

        await sut.SendRequestAsync(url, "GET", NoHeaders());

        fake.LastRequest!.RequestUri!.ToString().Should().Contain("id=5");
    }

    [Fact]
    public async Task SendRequestAsync_WithCustomHeaders_ForwardsThemToRequest()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync(
            "http://test.local/api", "GET",
            Headers(("X-Correlation-Id", "abc-123"), ("Accept-Language", "en")));

        fake.LastRequest!.Headers.Should().ContainSingle(h => h.Key == "X-Correlation-Id");
    }

    [Fact]
    public async Task SendRequestAsync_CustomHeader_IsForwarded()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com", "GET",
            Headers(("X-Api-Key", "secret-123")));

        fake.LastRequest!.Headers
            .Should().ContainSingle(h => h.Key == "X-Api-Key" && h.Value.Contains("secret-123"));
    }

    [Fact]
    public async Task SendRequestAsync_MultipleCustomHeaders_AllForwarded()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com", "GET",
            Headers(("X-Trace-Id", "trace-1"),
                ("Accept-Language", "ru-RU"),
                ("Authorization", "Bearer token")));

        var headers = fake.LastRequest!.Headers;
        headers.Should().ContainSingle(h => h.Key == "X-Trace-Id");
        headers.Should().ContainSingle(h => h.Key == "Accept-Language");
        headers.Should().ContainSingle(h => h.Key == "Authorization");
    }

    [Fact]
    public async Task SendRequestAsync_DuplicateHeaderKeys_BothValuesForwarded()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com", "GET",
            Headers(("X-Custom", "val1"), ("X-Custom", "val2")));

        var values = fake.LastRequest!.Headers
            .FirstOrDefault(h => h.Key == "X-Custom").Value;

        values.Should().Contain("val1");
        values.Should().Contain("val2");
    }

    [Fact]
    public async Task SendRequestAsync_ConnectionHeaderInInput_IsReplacedWithClose()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        // Send Keep-Alive header (must be ignored)
        await sut.SendRequestAsync(
            "http://test.local/api", "GET",
            Headers(("Connection", "keep-alive")));

        var connectionValues = fake.LastRequest!.Headers.Connection;
        connectionValues.Should().Contain("close");
        connectionValues.Should().NotContain("keep-alive");
    }


    [Fact]
    public async Task SendRequestAsync_WithBody_SetsContentOnRequest()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync(
            "http://test.local/api", "POST", NoHeaders(),
            body: "{\"name\":\"test\"}", contentType: "application/json");

        var sentBody = await fake.LastRequest!.Content!.ReadAsStringAsync();
        sentBody.Should().Be("{\"name\":\"test\"}");
    }

    [Fact]
    public async Task SendRequestAsync_WithBody_UsesSpecifiedContentType()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync(
            "http://test.local/api", "POST", NoHeaders(),
            body: "name=test", contentType: "application/x-www-form-urlencoded");

        fake.LastRequest!.Content!.Headers.ContentType!.MediaType
            .Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task SendRequestAsync_WithBody_DefaultsToApplicationJson()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync(
            "http://test.local/api", "POST", NoHeaders(),
            body: "{}", contentType: null);

        fake.LastRequest!.Content!.Headers.ContentType!.MediaType
            .Should().Be("application/json");
    }

    [Fact]
    public async Task SendRequestAsync_NoBody_RequestContentIsNull()
    {
        var fake = FakeHttpMessageHandler.ReturnJson("{}");
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://test.local/api", "GET", NoHeaders(), body: null);

        fake.LastRequest!.Content.Should().BeNull();
    }

    [Fact]
    public async Task SendRequestAsync_WithJsonBody_SetsContentCorrectly()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com", "POST", NoHeaders(),
            body: "{\"name\":\"test\",\"value\":42}",
            contentType: "application/json");

        var sentBody = await fake.LastRequest!.Content!.ReadAsStringAsync();
        sentBody.Should().Be("{\"name\":\"test\",\"value\":42}");
    }

    [Fact]
    public async Task SendRequestAsync_WithBody_ContentTypeHeaderIsSet()
    {
        var fake = FakeHttpMessageHandler.ReturnJson();
        using var sut = CreateSut(fake);

        await sut.SendRequestAsync("http://api.example.com", "POST", NoHeaders(),
            body: "data", contentType: "application/xml");

        fake.LastRequest!.Content!.Headers.ContentType!.MediaType
            .Should().Be("application/xml");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendRequestAsync_VariousStatusCodes_ReturnedAsIs(HttpStatusCode code)
    {
        using var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}", code));

        var response = await sut.SendRequestAsync("http://test.local/api", "GET", NoHeaders());

        response.StatusCode.Should().Be(code);
    }

    [Fact]
    public async Task SendRequestAsync_HandlerThrowsHttpRequestException_Propagates()
    {
        using var sut = CreateSut(
            FakeHttpMessageHandler.Throw(new HttpRequestException("network error")));

        Func<Task> act = () =>
            sut.SendRequestAsync("http://test.local/api", "GET", NoHeaders());

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*network error*");
    }


    [Fact]
    public async Task SendRequestAsync_WithInvalidUrl_ThrowsException()
    {
        // Use real http to check
        using var sut = new HttpService();

        Func<Task> act = () =>
            sut.SendRequestAsync("not-a-url", "GET", NoHeaders());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendRequestAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));

        Func<Task> act = () =>
            sut.SendRequestAsync("http://test.local/api", "GET",
                NoHeaders(), cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendRequestAsync_CalledTwice_LastRequestMetricsIsFromSecondCall()
    {
        using var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));

        await sut.SendRequestAsync("http://test.local/first", "GET", NoHeaders());
        var firstMetrics = sut.LastRequestMetrics;

        await sut.SendRequestAsync("http://test.local/second", "GET", NoHeaders());
        var secondMetrics = sut.LastRequestMetrics;

        secondMetrics.Should().NotBeSameAs(firstMetrics);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var sut = CreateSut(FakeHttpMessageHandler.ReturnJson("{}"));

        Action act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }

    public void Dispose() => _defaultSut.Dispose();
}