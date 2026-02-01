using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models;

namespace AvalonHttp.Services.Interfaces;

public interface IHttpService
{
    Task<HttpResponseMessage> SendRequestAsync(
        string url, 
        string method, 
        IEnumerable<KeyValuePair<string, string>> headers,
        string? body = null,
        string? contentType = null,
        CancellationToken cancellationToken = default);
    
    RequestMetrics LastRequestMetrics { get; }
}