using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class RequestViewModel : ViewModelBase, IDisposable
{
    private readonly IHttpService _httpService;

    [ObservableProperty]
    private string _requestUrl = "https://jsonplaceholder.typicode.com/users";

    [ObservableProperty]
    private string _selectedMethod = "GET";

    [ObservableProperty]
    private string _requestBody = "";

    [ObservableProperty]
    private string _responseContent = "";
    
    [ObservableProperty]
    private string _rawResponseContent = string.Empty;

    [ObservableProperty]
    private string _statusCode = "Ready";

    [ObservableProperty]
    private string _responseTime = "--";

    [ObservableProperty]
    private string _responseSize = "--";

    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private bool _isPrettyFormat = true;

    [ObservableProperty]
    private IBrush _statusBrush = new SolidColorBrush(Color.Parse("#6B7280"));
    
    [ObservableProperty]
    private bool _isPrettyActive  = true;

    [ObservableProperty]
    private bool _isRawActive = false;

    [RelayCommand]
    private void SetPrettyFormat()
    {
        Debug.WriteLine("SetPrettyFormat called");
        IsPrettyFormat = true;
        IsPrettyActive = true;
        IsRawActive = false;
        ApplyFormat();
        Debug.WriteLine($"After SetPrettyFormat - ResponseContent length: {ResponseContent?.Length}");
    }

    [RelayCommand]
    private void SetRawFormat()
    {
        Debug.WriteLine("SetRawFormat called");
        IsPrettyFormat = false;
        IsPrettyActive = false;
        IsRawActive = true;
        ApplyFormat();
        Debug.WriteLine($"After SetRawFormat - ResponseContent length: {ResponseContent?.Length}");
    }

    [ObservableProperty]
    private ObservableCollection<TimelineStageModel> _timelineStages = new();
    
    public int ResponseHeadersCount => ResponseHeaders.Count;
    public int ResponseCookiesCount => ResponseCookies.Count;
    
    [ObservableProperty]
    private double _totalRequestTime;
    
    public bool IsPrettyActive => IsPrettyFormat;
    public bool IsRawActive => !IsPrettyFormat;
    
    public HeadersViewModel HeadersViewModel { get; }
    public QueryParamsViewModel QueryParamsViewModel { get; }
    public AuthViewModel AuthViewModel { get; }

    public RequestViewModel(
        IHttpService httpService,
        HeadersViewModel headersViewModel,
        QueryParamsViewModel queryParamsViewModel,
        AuthViewModel authViewModel)
    {
        _httpService = httpService;
        HeadersViewModel = headersViewModel;
        QueryParamsViewModel = queryParamsViewModel;
        AuthViewModel = authViewModel;

        // Subscribe to URL changes from query params
        QueryParamsViewModel.UrlChanged += OnQueryParamsUrlChanged;
        
        // Load initial params from URL
        QueryParamsViewModel.LoadFromUrl(RequestUrl);
    }

    partial void OnRequestUrlChanged(string value)
    {
        QueryParamsViewModel.LoadFromUrl(value);
    }

    private void OnQueryParamsUrlChanged(object? sender, string e)
    {
        var baseUrl = RequestUrl.Split('?')[0];
        RequestUrl = QueryParamsViewModel.BuildUrl(baseUrl);
    }

    [RelayCommand]
    private async Task SendRequest()
    {
        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            StatusCode = "Error: URL is empty";
            StatusBrush = new SolidColorBrush(Color.Parse("#EF4444"));
            return;
        }

        try
        {
            IsLoading = true;
            StatusCode = "Sending...";
            ResponseContent = "";
            RawResponseContent = "";
            ResponseHeaders.Clear();
            ResponseCookies.Clear();
            TimelineStages.Clear();
            TotalRequestTime = 0;
            
            StatusCode = "Sending...";
            StatusBrush = new SolidColorBrush(Color.Parse("#F59E0B"));

            var startTime = DateTime.Now;

            var headers = HeadersViewModel.GetEnabledHeaders();
            var authHeaders = AuthViewModel.GetAuthHeaders();
        
            foreach (var authHeader in authHeaders)
            {
                headers[authHeader.Key] = authHeader.Value;
            }
            
            var authQueryParams = AuthViewModel.GetAuthQueryParams();
            if (authQueryParams.Count > 0)
            {
                var uriBuilder = new UriBuilder(RequestUrl);
                var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            
                foreach (var param in authQueryParams)
                {
                    query[param.Key] = param.Value;
                }
            
                uriBuilder.Query = query.ToString();
                RequestUrl = uriBuilder.ToString();
            }
            
            var response = await _httpService.SendRequestAsync(
                RequestUrl,
                SelectedMethod,
                headers,
                RequestBody);

            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalMilliseconds;

            ResponseTime = $"{duration:F0} ms";
            
            BuildTimeline(duration);

            var statusCode = (int)response.StatusCode;
            StatusCode = $"{statusCode} {response.StatusCode}";

            StatusBrush = statusCode >= 200 && statusCode < 300
                ? new SolidColorBrush(Color.Parse("#10B981"))
                : statusCode >= 400
                    ? new SolidColorBrush(Color.Parse("#EF4444"))
                    : new SolidColorBrush(Color.Parse("#F59E0B"));

            var content = await response.Content.ReadAsStringAsync();
            var contentLength = Encoding.UTF8.GetByteCount(content);

            ResponseSize = FormatBytes(contentLength);
            RawResponseContent = content;
            
            ApplyFormat();
        }
        catch (Exception ex)
        {
            StatusCode = "Error";
            ResponseContent = $"Error: {ex.Message}";
            StatusBrush = new SolidColorBrush(Color.Parse("#EF4444"));
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void ApplyFormat()
    {
        Debug.WriteLine($"ApplyFormat - IsPrettyFormat: {IsPrettyFormat}, RawContent length: {RawResponseContent?.Length}");
        
        if (string.IsNullOrWhiteSpace(RawResponseContent))
        {
            ResponseContent = string.Empty;
            Debug.WriteLine("ApplyFormat - RawResponseContent is empty");
            return;
        }

        try
        {
            if (IsPrettyFormat)
            {
                Debug.WriteLine("ApplyFormat - Formatting as Pretty");
                var jsonDoc = JsonDocument.Parse(RawResponseContent);
                var formatted = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                ResponseContent = formatted;
                Debug.WriteLine($"ApplyFormat - Pretty formatted, length: {formatted.Length}");
            }
            else
            {
                Debug.WriteLine("ApplyFormat - Using Raw format");
                ResponseContent = RawResponseContent;
                Debug.WriteLine($"ApplyFormat - Raw set, length: {RawResponseContent.Length}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyFormat - Exception: {ex.Message}");
            ResponseContent = RawResponseContent;
        }
    }

    private string FormatJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";

        try
        {
            var parsedJson = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return json;
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
    
    private void BuildTimeline(double totalTime)
    {
        TimelineStages.Clear();
        TotalRequestTime = totalTime;

        var metrics = _httpService.LastRequestMetrics;

        var stages = new[]
        {
            new TimelineStageModel 
            { 
                Name = "DNS Lookup", 
                Duration = metrics.DnsLookup, 
                Color = "#10B981" 
            },
            new TimelineStageModel 
            { 
                Name = "TCP Handshake", 
                Duration = metrics.TcpHandshake, 
                Color = "#3B82F6" 
            },
            new TimelineStageModel 
            { 
                Name = "SSL Handshake", 
                Duration = metrics.SslHandshake, 
                Color = "#F59E0B" 
            },
            new TimelineStageModel 
            { 
                Name = "Waiting (TTFB)", 
                Duration = metrics.TimeToFirstByte, 
                Color = "#EF4444" 
            },
            new TimelineStageModel 
            { 
                Name = "Content Download", 
                Duration = metrics.ContentDownload, 
                Color = "#06B6D4" 
            }
        };

        foreach (var stage in stages)
        {
            stage.DurationText = stage.Duration > 0 ? $"{stage.Duration:F2} ms" : "0 ms";
            stage.WidthPercent = totalTime > 0 ? (stage.Duration / totalTime) * 100 : 0;
            TimelineStages.Add(stage);
        }
    }

    public void Dispose()
    {
        if (QueryParamsViewModel != null)
        {
            QueryParamsViewModel.UrlChanged -= OnQueryParamsUrlChanged;
        }
        
        QueryParamsViewModel?.Dispose();
        
        (_httpService as IDisposable)?.Dispose();
    }
}
