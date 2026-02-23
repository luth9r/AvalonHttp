using System.Net;
using System.Text;

namespace AvalonHttpTests.Helpers;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handlerFunc;
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) => _handlerFunc = handlerFunc;
    
    public static FakeHttpMessageHandler ReturnJson(
        string json = "{}", HttpStatusCode code = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(code)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    
    public static FakeHttpMessageHandler ReturnEmpty(HttpStatusCode code = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(code)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json")
        });
    

    public static FakeHttpMessageHandler Throw(Exception ex)
        => new(_ => throw ex);
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_handlerFunc(request));
    }
}