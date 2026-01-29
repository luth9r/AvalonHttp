namespace AvalonHttp.Models;

public class ApiRequest
{
    public string Name { get; set; } = "New Request";
    public string Url { get; set; } = "";
    public HttpMethod Method { get; set; } = HttpMethod.GET;
}