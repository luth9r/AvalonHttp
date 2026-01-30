using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace AvalonHttp.Services.Interfaces;

public interface IHttpService
{
    Task<HttpResponseMessage> SendRequestAsync(
        string url, 
        string method, 
        Dictionary<string, string> headers,
        string? body = null);
}