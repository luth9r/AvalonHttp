using System;
using System.Collections.Generic;
using System.Linq;
using AvalonHttp.Models;

namespace AvalonHttp.Services.Interfaces;

public class UrlParserService : IUrlParserService
{
    public (string baseUrl, List<QueryParameter> parameters) ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (string.Empty, new List<QueryParameter>());
        }

        var parts = url.Split('?', 2);
        var baseUrl = parts[0];
        var parameters = new List<QueryParameter>();

        if (parts.Length == 2)
        {
            var queryString = parts[1];
            var paramPairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in paramPairs)
            {
                var keyValue = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : "";

                parameters.Add(new QueryParameter
                {
                    IsEnabled = true,
                    Key = key,
                    Value = value
                });
            }
        }

        return (baseUrl, parameters);
    }

    public string BuildUrl(string baseUrl, IEnumerable<QueryParameter> parameters)
    {
        var enabledParams = parameters
            .Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key))
            .ToList();

        if (!enabledParams.Any())
        {
            return baseUrl;
        }

        var queryString = string.Join("&", enabledParams.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value ?? "")}"));

        return $"{baseUrl}?{queryString}";
    }
}