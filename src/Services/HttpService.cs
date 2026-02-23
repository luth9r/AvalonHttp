using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class HttpService : IHttpService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, RequestMetrics> _metricsStore = new();
    
    public RequestMetrics? LastRequestMetrics { get; private set; }

    public HttpService()
    {
        // Reuse HttpClient with custom handler
        var metricsHandler = new MetricsHandler(_metricsStore);
        _httpClient = new HttpClient(metricsHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Used only for testing purposes.
    /// </summary>
    public HttpService(HttpMessageHandler? innerHandler = null)
    {
        var metricsHandler = new MetricsHandler(_metricsStore)
        {
            InnerHandler = innerHandler ?? new HttpClientHandler() 
        };
    
        _httpClient = new HttpClient(metricsHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<HttpResponseMessage> SendRequestAsync(
        string url,
        string method,
        IEnumerable<KeyValuePair<string, string>> headers,
        string? body = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var totalStopwatch = Stopwatch.StartNew();

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        
        // Store request ID for metrics correlation
        request.Options.Set(new HttpRequestOptionsKey<string>("MetricsRequestId"), requestId);

        request.Headers.Connection.Add("close");
        
        // Add headers
        foreach (var header in headers)
        {
            if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Add body with flexible content type
        if (!string.IsNullOrEmpty(body))
        {
            var mediaType = contentType ?? "application/json";
            request.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        // Send request and measure TTFB
        var response = await _httpClient.SendAsync(
            request, 
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        
        var ttfb = totalStopwatch.Elapsed.TotalMilliseconds;

        // Download content
        await response.Content.ReadAsStringAsync(cancellationToken);
        totalStopwatch.Stop();

        // Calculate metrics
        var metrics = new RequestMetrics();
        
        if (_metricsStore.TryRemove(requestId, out var connectionMetrics))
        {
            metrics.DnsLookup = connectionMetrics.DnsLookup;
            metrics.TcpHandshake = connectionMetrics.TcpHandshake;
            metrics.SslHandshake = connectionMetrics.SslHandshake;
        }

        var connectionTime = metrics.DnsLookup + metrics.TcpHandshake + metrics.SslHandshake;
        metrics.TimeToFirstByte = Math.Max(0, ttfb - connectionTime);
        metrics.ContentDownload = totalStopwatch.Elapsed.TotalMilliseconds - ttfb;

        LastRequestMetrics = metrics;
        return response;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
