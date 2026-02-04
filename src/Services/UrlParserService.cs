using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class UrlParserService : IUrlParserService
{
    public (string baseUrl, List<KeyValueItemModel> parameters) ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (string.Empty, new List<KeyValueItemModel>());
        }

        try
        {
            // Remove fragment (#anchor)
            var fragmentIndex = url.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                url = url[..fragmentIndex];
            }

            string baseUrl;
            string queryString = string.Empty;
            
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                baseUrl = uri.GetLeftPart(UriPartial.Path);
                
                if (!string.IsNullOrEmpty(uri.Query))
                {
                    queryString = uri.Query.Length > 1 ? uri.Query[1..] : string.Empty;
                }
            }
            else
            {
                var parts = url.Split('?', 2);
                baseUrl = parts[0];
                queryString = parts.Length > 1 ? parts[1] : string.Empty;
            }
            
            var parameters = ParseQueryString(queryString);

            return (baseUrl, parameters);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse URL: {ex.Message}");
            return (url, new List<KeyValueItemModel>());
        }
    }

    private List<KeyValueItemModel> ParseQueryString(string query)
    {
        var result = new List<KeyValueItemModel>();
        
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = parts[0];
            var value = parts.Length > 1 ? parts[1] : string.Empty;

            result.Add(new KeyValueItemModel
            {
                IsEnabled = true,
                Key = DecodeQueryParam(key),
                Value = DecodeQueryParam(value)
            });
        }

        return result;
    }

    private string DecodeQueryParam(string param)
    {
        if (string.IsNullOrEmpty(param))
        {
            return string.Empty;
        }

        try
        {
            return Uri.UnescapeDataString(param.Replace("+", " "));
        }
        catch
        {
            return param;
        }
    }

    public string BuildUrl(string baseUrl, IEnumerable<KeyValueItemModel> parameters)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        // Filter empty keys
        var enabledParams = parameters
            .Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key))
            .ToList();

        if (enabledParams.Count == 0)
        {
            return baseUrl;
        }

        // Pre-calculate capacity to avoid reallocations
        var estimatedLength = baseUrl.Length + 1; // +1 for '?' or '&'
        foreach (var p in enabledParams)
        {
            estimatedLength += (p.Key?.Length ?? 0) + (p.Value?.Length ?? 0) + 2; // +2 for '=' and '&'
        }

        var sb = new StringBuilder(estimatedLength + enabledParams.Count * 10); // +10% buffer for encoding
        
        sb.Append(baseUrl);
        sb.Append(baseUrl.Contains('?') ? '&' : '?');

        for (int i = 0; i < enabledParams.Count; i++)
        {
            var p = enabledParams[i];
            
            if (i > 0)
            {
                sb.Append('&');
            }

            sb.Append(EncodeQueryParam(p.Key));
            sb.Append('=');
            sb.Append(EncodeQueryParam(p.Value));
        }

        return sb.ToString();
    }

    private string EncodeQueryParam(string? param)
    {
        if (string.IsNullOrEmpty(param))
        {
            return string.Empty;
        }

        try
        {
            // Handle long strings (Uri.EscapeDataString has 32k limit)
            if (param.Length > 32000)
            {
                var sb = new StringBuilder(param.Length * 2); // Estimate 2x for encoded size
                
                for (int i = 0; i < param.Length; i += 32000)
                {
                    var chunk = param.Substring(i, Math.Min(32000, param.Length - i));
                    sb.Append(Uri.EscapeDataString(chunk));
                }
                
                return sb.ToString();
            }

            return Uri.EscapeDataString(param);
        }
        catch
        {
            return param;
        }
    }
}
