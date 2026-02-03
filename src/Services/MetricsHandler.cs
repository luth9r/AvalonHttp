using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models;

namespace AvalonHttp.Services;

public class MetricsHandler : DelegatingHandler
{
    private readonly ConcurrentDictionary<string, RequestMetrics> _metricsStore;

    public MetricsHandler(ConcurrentDictionary<string, RequestMetrics> metricsStore) 
        : base(CreateSocketsHandler())
    {
        _metricsStore = metricsStore;
        
        if (InnerHandler is SocketsHttpHandler socketsHandler)
        {
            socketsHandler.ConnectCallback = ConnectCallback;
        }
    }

    private static SocketsHttpHandler CreateSocketsHandler()
    {
        return new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.Zero,
            PooledConnectionIdleTimeout = TimeSpan.Zero,
            EnableMultipleHttp2Connections = true,
            MaxConnectionsPerServer = 10,
            // Enable automatic decompression
            AutomaticDecompression = DecompressionMethods.All
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Extract request ID for metrics correlation
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<string>("MetricsRequestId"), 
            out var requestId);

        return await base.SendAsync(request, cancellationToken);
    }

    private async ValueTask<Stream> ConnectCallback(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var metrics = new RequestMetrics();
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. DNS Lookup
            var dnsStart = sw.Elapsed.TotalMilliseconds;
            var addresses = await Dns.GetHostAddressesAsync(
                context.DnsEndPoint.Host, 
                cancellationToken);
            metrics.DnsLookup = sw.Elapsed.TotalMilliseconds - dnsStart;

            // 2. TCP Handshake
            var tcpStart = sw.Elapsed.TotalMilliseconds;
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) 
            { 
                NoDelay = true 
            };

            try
            {
                await socket.ConnectAsync(
                    addresses, 
                    context.DnsEndPoint.Port, 
                    cancellationToken);
                metrics.TcpHandshake = sw.Elapsed.TotalMilliseconds - tcpStart;
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            var networkStream = new NetworkStream(socket, ownsSocket: true);

            // 3. SSL/TLS Handshake (if HTTPS)
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

                    metrics.SslHandshake = sw.Elapsed.TotalMilliseconds - sslStart;

                    // Store metrics for this request
                    StoreMetrics(context.InitialRequestMessage, metrics);
                    
                    return sslStream;
                }
                catch
                {
                    await sslStream.DisposeAsync();
                    throw;
                }
            }

            // Store metrics for HTTP request
            StoreMetrics(context.InitialRequestMessage, metrics);
            return networkStream;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Connection failed: {ex.Message}");
            throw;
        }
    }

    private void StoreMetrics(HttpRequestMessage request, RequestMetrics metrics)
    {
        if (request.Options.TryGetValue(
            new HttpRequestOptionsKey<string>("MetricsRequestId"), 
            out var requestId))
        {
            _metricsStore[requestId] = metrics;
        }
    }
}
