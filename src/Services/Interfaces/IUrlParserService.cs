using System.Collections.Generic;
using AvalonHttp.Models;

namespace AvalonHttp.Services.Interfaces;

public interface IUrlParserService
{
    (string baseUrl, List<KeyValueItemModel> parameters) ParseUrl(string url);
    
    string BuildUrl(string baseUrl, IEnumerable<KeyValueItemModel> parameters);
}