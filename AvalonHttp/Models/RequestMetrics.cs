namespace AvalonHttp.Models;

public class RequestMetrics
{
    public double DnsLookup { get; set; }
    public double TcpHandshake { get; set; }
    public double SslHandshake { get; set; }
    public double TimeToFirstByte { get; set; }
    public double ContentDownload { get; set; }
    public double Total { get; set; }
}