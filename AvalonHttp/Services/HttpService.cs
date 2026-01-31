using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class HttpService : IHttpService, IDisposable
{
    private RequestMetrics _lastMetrics = new();

    public RequestMetrics LastRequestMetrics => _lastMetrics;

    public async Task<HttpResponseMessage> SendRequestAsync(
        string url,
        string method,
        Dictionary<string, string> headers,
        string body)
    {
        var metricsHandler = new MetricsHandler();
        using var client = new HttpClient(metricsHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        var totalStopwatch = Stopwatch.StartNew();

        var request = new HttpRequestMessage(new HttpMethod(method), url);

        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var ttfb = totalStopwatch.Elapsed.TotalMilliseconds;

        await response.Content.ReadAsStringAsync();
        totalStopwatch.Stop();

        _lastMetrics.DnsLookup = metricsHandler.DnsTime;
        _lastMetrics.TcpHandshake = metricsHandler.TcpTime;
        _lastMetrics.SslHandshake = metricsHandler.SslTime;

        var connectionTime = _lastMetrics.DnsLookup + _lastMetrics.TcpHandshake + _lastMetrics.SslHandshake;
        _lastMetrics.TimeToFirstByte = Math.Max(0, ttfb - connectionTime);
        _lastMetrics.ContentDownload = totalStopwatch.Elapsed.TotalMilliseconds - ttfb;

        return response;
    }

    public void Dispose()
    {
    }
}

public class MetricsHandler : DelegatingHandler
{
    private double _dnsTime;
    private double _tcpTime;
    private double _sslTime;

    public double DnsTime => _dnsTime;
    public double TcpTime => _tcpTime;
    public double SslTime => _sslTime;

    public MetricsHandler() : base(CreateSocketsHandler())
    {
        if (InnerHandler is SocketsHttpHandler socketsHandler)
        {
            socketsHandler.ConnectCallback = ConnectCallback;
        }
    }

    private static SocketsHttpHandler CreateSocketsHandler()
    {
        return new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(90),
            EnableMultipleHttp2Connections = true,
            MaxConnectionsPerServer = 10
        };
    }

    private async ValueTask<Stream> ConnectCallback(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        _dnsTime = 0;
        _tcpTime = 0;
        _sslTime = 0;

        var sw = Stopwatch.StartNew();

        // 1. DNS Lookup
        var dnsStart = sw.Elapsed.TotalMilliseconds;
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        _dnsTime = sw.Elapsed.TotalMilliseconds - dnsStart;

        // 2. TCP Handshake
        var tcpStart = sw.Elapsed.TotalMilliseconds;
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            _tcpTime = sw.Elapsed.TotalMilliseconds - tcpStart;
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        var networkStream = new NetworkStream(socket, ownsSocket: true);
        
        if (context.InitialRequestMessage.RequestUri?.Scheme == "https")
        {
            var sslStart = sw.Elapsed.TotalMilliseconds;
            var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

            try
            {
                await sslStream.AuthenticateAsClientAsync(
                    context.DnsEndPoint.Host,
                    null,
                    System.Security.Authentication.SslProtocols.None,
                    checkCertificateRevocation: false);

                _sslTime = sw.Elapsed.TotalMilliseconds - sslStart;
                return sslStream;
            }
            catch
            {
                await sslStream.DisposeAsync();
                throw;
            }
        }

        return networkStream;
    }
}