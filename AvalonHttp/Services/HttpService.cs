using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class HttpService : IHttpService
{
    private static readonly HttpClient _sharedHttpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    public async Task<HttpResponseMessage> SendRequestAsync(
        string url, 
        string method, 
        Dictionary<string, string> headers,
        string? body = null)
    {
        using var request = new HttpRequestMessage(GetHttpMethod(method), url);

        bool hasUserAgent = false;
        
        // Add headers
        foreach (var header in headers)
        {
            if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                hasUserAgent = true;
            }
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        if (!hasUserAgent)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", "AvalonHttp/1.0");
        }

        // Add body if exists
        if (!string.IsNullOrWhiteSpace(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await _sharedHttpClient.SendAsync(request);
    }

    private static HttpMethod GetHttpMethod(string method)
    {
        return method switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => HttpMethod.Get
        };
    }
    
}