using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class RequestViewModel : ViewModelBase, IDisposable
{
    // ========================================
    // Constants
    // ========================================
    
    private static class StatusColors
    {
        public static readonly IBrush Ready = new SolidColorBrush(Color.Parse("#6B7280"));
        public static readonly IBrush Success = new SolidColorBrush(Color.Parse("#10B981"));
        public static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F59E0B"));
        public static readonly IBrush Error = new SolidColorBrush(Color.Parse("#EF4444"));
    }
    
    private static class TimelineColors
    {
        public const string DnsLookup = "#10B981";
        public const string TcpHandshake = "#3B82F6";
        public const string SslHandshake = "#F59E0B";
        public const string TimeToFirstByte = "#EF4444";
        public const string ContentDownload = "#06B6D4";
    }

    // ========================================
    // Static Collections
    // ========================================
    
    public static ObservableCollection<string> HttpMethods { get; } = new()
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    // ========================================
    // Fields
    // ========================================
    
    private readonly IHttpService _httpService;
    private ApiRequest? _activeRequest;
    private readonly IDirtyTrackerService _dirtyTracker;
    private string? _requestSnapshot;
    private bool _isLoadingData;
    private bool _isSyncingUrl;

    // ========================================
    // Observable Properties
    // ========================================
    
    [ObservableProperty]
    private string _selectedRequestTab = "Params";

    [ObservableProperty]
    private string _selectedResponseTab = "Body";
    
    [ObservableProperty]
    private string _name = "New Request";

    [ObservableProperty]
    private string _requestUrl = string.Empty;

    [ObservableProperty]
    private string _selectedMethod = "GET";

    [ObservableProperty]
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private string _responseContent = string.Empty;

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
    private string _responseContentType = "json";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrettyActive))]
    [NotifyPropertyChangedFor(nameof(IsRawActive))]
    private bool _isPrettyFormat = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCurrentRequestCommand))]
    private bool _isDirty;

    [ObservableProperty]
    private IBrush _statusBrush = StatusColors.Ready;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResponseHeadersCount))]
    private ObservableCollection<ResponseHeaderModel> _responseHeaders = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResponseCookiesCount))]
    private ObservableCollection<ResponseHeaderModel> _responseCookies = new();

    [ObservableProperty]
    private ObservableCollection<TimelineStageModel> _timelineStages = new();

    [ObservableProperty]
    private double _totalRequestTime;

    [ObservableProperty]
    private bool _hasResponseData;

    // ========================================
    // Computed Properties
    // ========================================
    
    public int ResponseHeadersCount => ResponseHeaders.Count;
    public int ResponseCookiesCount => ResponseCookies.Count;
    public bool IsPrettyActive => IsPrettyFormat;
    public bool IsRawActive => !IsPrettyFormat;

    // ========================================
    // Child ViewModels
    // ========================================
    
    public HeadersViewModel HeadersViewModel { get; }
    public QueryParamsViewModel QueryParamsViewModel { get; }
    public AuthViewModel AuthViewModel { get; }
    public CookiesViewModel CookiesViewModel { get; }
    public EnvironmentsViewModel EnvironmentsViewModel { get; }

    // ========================================
    // Events
    // ========================================
    
    public event EventHandler<ApiRequest>? RequestSaved;

    // ========================================
    // Constructor
    // ========================================
    
    public RequestViewModel(
        IHttpService httpService,
        HeadersViewModel headersViewModel,
        QueryParamsViewModel queryParamsViewModel,
        AuthViewModel authViewModel,
        CookiesViewModel cookiesViewModel,
        EnvironmentsViewModel environmentsViewModel,
        IDirtyTrackerService dirtyTracker)
    {
        _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
        HeadersViewModel = headersViewModel ?? throw new ArgumentNullException(nameof(headersViewModel));
        QueryParamsViewModel = queryParamsViewModel ?? throw new ArgumentNullException(nameof(queryParamsViewModel));
        AuthViewModel = authViewModel ?? throw new ArgumentNullException(nameof(authViewModel));
        CookiesViewModel = cookiesViewModel ?? throw new ArgumentNullException(nameof(cookiesViewModel));
        EnvironmentsViewModel = environmentsViewModel ?? throw new ArgumentNullException(nameof(environmentsViewModel));
        _dirtyTracker = dirtyTracker ?? throw new ArgumentNullException(nameof(dirtyTracker));

        SubscribeToEvents();
    }

    // ========================================
    // Event Subscription
    // ========================================
    
    private void SubscribeToEvents()
    {
        QueryParamsViewModel.UrlChanged += OnQueryParamsUrlChanged;
        AuthViewModel.PropertyChanged += OnAuthPropertyChanged;
        
        HeadersViewModel.Headers.CollectionChanged += OnHeadersCollectionChanged;
        QueryParamsViewModel.Parameters.CollectionChanged += OnParamsCollectionChanged;
        CookiesViewModel.Cookies.CollectionChanged += OnCookiesCollectionChanged;
    }

    private void UnsubscribeFromEvents()
    {
        if (_activeRequest != null)
        {
            _activeRequest.PropertyChanged -= OnActiveRequestPropertyChanged;
        }
        
        QueryParamsViewModel.UrlChanged -= OnQueryParamsUrlChanged;
        AuthViewModel.PropertyChanged -= OnAuthPropertyChanged;
        
        HeadersViewModel.Headers.CollectionChanged -= OnHeadersCollectionChanged;
        QueryParamsViewModel.Parameters.CollectionChanged -= OnParamsCollectionChanged;
        CookiesViewModel.Cookies.CollectionChanged -= OnCookiesCollectionChanged;
        
        UnsubscribeFromCollection(HeadersViewModel.Headers);
        UnsubscribeFromCollection(QueryParamsViewModel.Parameters);
        UnsubscribeFromCollection(CookiesViewModel.Cookies);
    }

    // ========================================
    // Event Handlers
    // ========================================
    
    private void OnAuthPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkAsDirty();
    }

    private void OnHeadersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleCollectionChanged(e);
    }

    private void OnParamsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleCollectionChanged(e);
    }

    private void OnCookiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleCollectionChanged(e);
    }

    private void HandleCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (KeyValueItemModel item in e.NewItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged; // Prevent duplicates
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        MarkAsDirty();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkAsDirty();
    }

    private void OnActiveRequestPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApiRequest.Name))
        {
            Name = _activeRequest!.Name;
            MarkAsDirty();
        }
    }

    private void OnQueryParamsUrlChanged(object? sender, string? e)
    {
        if (_isLoadingData || _isSyncingUrl) return;

        _isSyncingUrl = true;

        try
        {
            UpdateUrlFromQueryParams();
            MarkAsDirty();
        }
        finally
        {
            _isSyncingUrl = false;
        }
    }

    // ========================================
    // Property Change Handlers
    // ========================================
    
    partial void OnNameChanged(string value) => MarkAsDirty();
    partial void OnSelectedMethodChanged(string value) => MarkAsDirty();
    partial void OnRequestBodyChanged(string value) => MarkAsDirty();

    partial void OnRequestUrlChanged(string value)
    {
        if (_isSyncingUrl || _isLoadingData) return;

        QueryParamsViewModel.LoadFromUrl(value);
        ClearResponseData();
        MarkAsDirty();
    }

    // ========================================
    // Commands
    // ========================================
    
    [RelayCommand]
    private void SelectRequestTab(string? tabName) => SelectedRequestTab = tabName ?? "Params";

    [RelayCommand]
    private void SelectResponseTab(string? tabName) => SelectedResponseTab = tabName ?? "Body";
    
    [RelayCommand]
    private async Task SendRequest()
    {
        if (!ValidateRequest()) return;

        try
        {
            PrepareForRequest();
            
            var startTime = DateTime.Now;
            var response = await ExecuteHttpRequestAsync();
            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            await ProcessResponseAsync(response, duration);
        }
        catch (TaskCanceledException)
        {
            HandleRequestCancelled();
        }
        catch (HttpRequestException ex)
        {
            HandleNetworkError(ex);
        }
        catch (Exception ex)
        {
            HandleGeneralError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SetPrettyFormat()
    {
        IsPrettyFormat = true;
        ApplyFormat();
    }

    [RelayCommand]
    private void SetRawFormat()
    {
        IsPrettyFormat = false;
        ApplyFormat();
    }

    [RelayCommand(CanExecute = nameof(CanSaveRequest))]
    private void SaveCurrentRequest()
    {
        if (_activeRequest == null) return;
        
        SaveRequestData();
        _requestSnapshot = _dirtyTracker.TakeSnapshot(_activeRequest);
        IsDirty = false;
        
        RequestSaved?.Invoke(this, _activeRequest);
    }

    private bool CanSaveRequest() => IsDirty && _activeRequest != null;

    // ========================================
    // HTTP Request Methods
    // ========================================
    
    private bool ValidateRequest()
    {
        if (string.IsNullOrWhiteSpace(RequestUrl))
        {
            StatusCode = "Error: URL is empty";
            StatusBrush = StatusColors.Error;
            return false;
        }
        return true;
    }

    private void PrepareForRequest()
    {
        IsLoading = true;
        HasResponseData = false;
        ClearResponseCollections();
        
        StatusCode = "Sending...";
        StatusBrush = StatusColors.Warning;
    }

    private void ClearResponseCollections()
    {
        ResponseContent = string.Empty;
        RawResponseContent = string.Empty;
        ResponseHeaders.Clear();
        ResponseCookies.Clear();
        TimelineStages.Clear();
        TotalRequestTime = 0;
    }

    private async Task<HttpResponseMessage> ExecuteHttpRequestAsync()
    {
        var headersList = BuildHeadersList();
        var finalUrl = BuildFinalUrl();
        var finalBody = ResolveVariables(RequestBody);

        return await _httpService.SendRequestAsync(
            finalUrl,
            SelectedMethod,
            headersList,
            finalBody);
    }

    private List<KeyValuePair<string, string>> BuildHeadersList()
    {
        var headersList = new List<KeyValuePair<string, string>>();
    
        // Add custom headers (with variable resolution)
        headersList.AddRange(HeadersViewModel.GetEnabledHeaders(ResolveVariables));

        // Add cookies as Cookie header
        var cookieHeaderValue = CookiesViewModel.GetCookieHeaderValue(ResolveVariables);
        if (!string.IsNullOrWhiteSpace(cookieHeaderValue))
        {
            headersList.Add(new KeyValuePair<string, string>("Cookie", cookieHeaderValue));
        }

        // Add auth headers
        var authHeaders = AuthViewModel.GetAuthHeaders(ResolveVariables);
        headersList.AddRange(authHeaders);

        return headersList;
    }

    private string BuildFinalUrl()
    {
        var finalUrl = ResolveVariables(RequestUrl);
    
        // Add auth query params if needed
        var authQueryParams = AuthViewModel.GetAuthQueryParams(ResolveVariables);
    
        if (authQueryParams.Count > 0)
        {
            finalUrl = AppendQueryParams(finalUrl, authQueryParams);
        }

        return finalUrl;
    }

    private string AppendQueryParams(string url, Dictionary<string, string> queryParams)
    {
        try
        {
            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach (var param in queryParams)
            {
                query[param.Key] = param.Value;
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to append query params: {ex.Message}");
            return url;
        }
    }

    private async Task ProcessResponseAsync(HttpResponseMessage response, double duration)
    {
        ResponseTime = $"{duration:F0} ms";
        BuildTimeline(duration);

        var statusCode = (int)response.StatusCode;
        UpdateStatusDisplay(statusCode, response.StatusCode.ToString());

        var content = await response.Content.ReadAsStringAsync();
        ProcessResponseContent(content);
        LoadResponseHeaders(response);
        LoadResponseCookies(response);

        ApplyFormat();
        HasResponseData = true;
    }

    private void UpdateStatusDisplay(int statusCode, string statusText)
    {
        StatusCode = $"{statusCode} {statusText}";
        StatusBrush = statusCode switch
        {
            >= 200 and < 300 => StatusColors.Success,
            >= 400 => StatusColors.Error,
            _ => StatusColors.Warning
        };
    }

    private void ProcessResponseContent(string content)
    {
        var contentLength = Encoding.UTF8.GetByteCount(content);
        ResponseSize = FormatBytes(contentLength);
        RawResponseContent = content;
    }

    private void LoadResponseHeaders(HttpResponseMessage response)
    {
        foreach (var header in response.Headers)
        {
            ResponseHeaders.Add(new ResponseHeaderModel
            {
                Name = header.Key,
                Value = string.Join(", ", header.Value)
            });
        }

        foreach (var header in response.Content.Headers)
        {
            ResponseHeaders.Add(new ResponseHeaderModel
            {
                Name = header.Key,
                Value = string.Join(", ", header.Value)
            });
        }
        
        OnPropertyChanged(nameof(ResponseHeadersCount));
    }

    private void LoadResponseCookies(HttpResponseMessage response)
    {
        ResponseCookies.Clear();
        
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            ParseCookieValues(cookies);
        }
        
        if (response.Content.Headers.TryGetValues("Set-Cookie", out var contentCookies))
        {
            ParseCookieValues(contentCookies);
        }
        
        OnPropertyChanged(nameof(ResponseCookiesCount));
    }
    
    private void ParseCookieValues(IEnumerable<string> cookieValues)
    {
        foreach (var cookie in cookieValues)
        {
            var firstPart = cookie.Split(';')[0];
            var parts = firstPart.Split('=', 2);
        
            if (parts.Length >= 2)
            {
                ResponseCookies.Add(new ResponseHeaderModel
                {
                    Name = parts[0].Trim(),
                    Value = parts[1].Trim()
                });
            }
            else if (parts.Length == 1)
            {
                ResponseCookies.Add(new ResponseHeaderModel
                {
                    Name = parts[0].Trim(),
                    Value = ""
                });
            }
        }
    }

    private void HandleRequestCancelled()
    {
        StatusCode = "Cancelled";
        RawResponseContent = "Request was cancelled";
        ResponseContent = RawResponseContent;
        StatusBrush = StatusColors.Warning;
        HasResponseData = false;
    }

    private void HandleNetworkError(HttpRequestException ex)
    {
        StatusCode = "Network Error";
        RawResponseContent = $"Network error: {ex.Message}";
        ResponseContent = RawResponseContent;
        StatusBrush = StatusColors.Error;
        HasResponseData = false;
    }

    private void HandleGeneralError(Exception ex)
    {
        StatusCode = "Error";
        RawResponseContent = $"Error: {ex.Message}";
        ResponseContent = RawResponseContent;
        StatusBrush = StatusColors.Error;
        HasResponseData = false;
    }

    // ========================================
    // Request Data Management
    // ========================================
    
    /// <summary>
    /// Load request data into the ViewModel
    /// </summary>
    public void LoadRequest(ApiRequest request)
    {
        _isLoadingData = true;

        try
        {
            UnsubscribeFromActiveRequest();
            
            System.Diagnostics.Debug.WriteLine($"🔵 LoadRequest:");
            System.Diagnostics.Debug.WriteLine($"   Request ID: {request.Id}");
            System.Diagnostics.Debug.WriteLine($"   Request hash: {request.GetHashCode()}");
            System.Diagnostics.Debug.WriteLine($"   Body: '{request.Body}'");
            
            _activeRequest = request;
            _activeRequest.PropertyChanged += OnActiveRequestPropertyChanged;

            LoadBasicProperties(request);
            LoadCollections(request);
            LoadAuthData(request.AuthData);

            ClearResponseData();
            _requestSnapshot = _dirtyTracker.TakeSnapshot(_activeRequest);
            IsDirty = false;
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    private void UnsubscribeFromActiveRequest()
    {
        if (_activeRequest != null)
        {
            _activeRequest.PropertyChanged -= OnActiveRequestPropertyChanged;
        }
    }

    private void LoadBasicProperties(ApiRequest request)
    {
        Name = request.Name;
        RequestUrl = request.Url;
        SelectedMethod = request.MethodString;
        RequestBody = request.Body ?? string.Empty;
    }

    private void LoadCollections(ApiRequest request)
    {
        LoadCollection(HeadersViewModel.Headers, request.Headers);
        LoadCollection(CookiesViewModel.Cookies, request.Cookies);
        
        QueryParamsViewModel.LoadFromUrl(request.Url);
        
        // Subscribe to all items
        SubscribeToCollection(HeadersViewModel.Headers);
        SubscribeToCollection(QueryParamsViewModel.Parameters);
        SubscribeToCollection(CookiesViewModel.Cookies);
    }

    private void LoadCollection(
        ObservableCollection<KeyValueItemModel> target,
        ObservableCollection<KeyValueItemModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(new KeyValueItemModel
            {
                Key = item.Key ?? string.Empty,
                Value = item.Value ?? string.Empty,
                IsEnabled = item.IsEnabled
            });
        }
    }

    private void SubscribeToCollection(ObservableCollection<KeyValueItemModel> collection)
    {
        foreach (var item in collection)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void LoadAuthData(AuthData? authData)
    {
        AuthViewModel.LoadFromAuthData(authData);
    }

    private void SaveRequestData()
    {
        if (_activeRequest == null) return;

        System.Diagnostics.Debug.WriteLine($"🟢 SaveRequestData START:");
        System.Diagnostics.Debug.WriteLine($"   Request ID: {_activeRequest.Id}");
        System.Diagnostics.Debug.WriteLine($"   Request hash: {_activeRequest.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"   Name: {Name} -> {_activeRequest.Name}");
        System.Diagnostics.Debug.WriteLine($"   URL: {RequestUrl}");
        System.Diagnostics.Debug.WriteLine($"   Body BEFORE: '{_activeRequest.Body}'");
        
        _activeRequest.Name = Name;
        _activeRequest.Url = RequestUrl;
        _activeRequest.MethodString = SelectedMethod;
        _activeRequest.Body = RequestBody;

        System.Diagnostics.Debug.WriteLine($"   Body AFTER: '{_activeRequest.Body}'");
        
        SaveCollection(_activeRequest.Headers, HeadersViewModel.Headers);
        SaveCollection(_activeRequest.QueryParameters, QueryParamsViewModel.Parameters);
        SaveCollection(_activeRequest.Cookies, CookiesViewModel.Cookies);
        
        _activeRequest.AuthData = AuthViewModel.ToAuthData();
        
        System.Diagnostics.Debug.WriteLine($"🟢 SaveRequestData END");
    }

    private void SaveCollection(
        ObservableCollection<KeyValueItemModel> target,
        ObservableCollection<KeyValueItemModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(new KeyValueItemModel
            {
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            });
        }
    }
    
    [RelayCommand]
    private async Task SaveResponse()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.StorageProvider == null) return;

                var fileTypeFilter = new Avalonia.Platform.Storage.FilePickerFileType("JSON File")
                {
                    Patterns = new[] { "*.json" },
                    MimeTypes = new[] { "application/json" }
                };

                var safeFileName = string.Join("_", (Name ?? "response").Split(System.IO.Path.GetInvalidFileNameChars()));
            
                var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Save Response",
                    SuggestedFileName = $"{safeFileName}.json",
                    FileTypeChoices = new[] { fileTypeFilter },
                    DefaultExtension = "json"
                });

                if (file != null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new System.IO.StreamWriter(stream);
                    await writer.WriteAsync(ResponseContent);
                    System.Diagnostics.Debug.WriteLine($"Response saved to: {file.Path}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save response: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyResponse()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.Clipboard != null)
                {
                    await mainWindow.Clipboard.SetTextAsync(ResponseContent);
                    System.Diagnostics.Debug.WriteLine("Response copied to clipboard");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
        }
    }
    
    // ========================================
    // Helper Methods
    // ========================================
    
    private string GetResponseType()
    {
        var contentType = ResponseHeaders.FirstOrDefault(h => 
            h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value ?? "";

        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return "json";
        if (contentType.Contains("application/xml", StringComparison.OrdinalIgnoreCase) || 
            contentType.Contains("text/xml", StringComparison.OrdinalIgnoreCase)) return "xml";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)) return "html";

        return "text";
    }
    
    private string ResolveVariables(string text)
    {
        return EnvironmentsViewModel.ResolveVariables(text);
    }
    
    private void UpdateUrlFromQueryParams()
    {
        var currentUrl = RequestUrl ?? string.Empty;
        var baseUrl = currentUrl.Split('?')[0];

        if (string.IsNullOrWhiteSpace(baseUrl)) return;

        var newUrl = QueryParamsViewModel.BuildUrl(baseUrl);
    
        // Decode brackets for readability: %7B%7BVar%7D%7D → {{Var}}
        newUrl = newUrl.Replace("%7B", "{").Replace("%7D", "}");

        if (RequestUrl != newUrl)
        {
            RequestUrl = newUrl;
        }
    }

    private void ClearResponseData()
    {
        if (!HasResponseData) return;

        HasResponseData = false;
        ResponseContent = string.Empty;
        RawResponseContent = string.Empty;
        ResponseHeaders.Clear();
        ResponseCookies.Clear();
        TimelineStages.Clear();
        TotalRequestTime = 0;
        StatusCode = "Ready";
        ResponseTime = "--";
        ResponseSize = "--";
        StatusBrush = StatusColors.Ready;
    }

    private void ApplyFormat()
    {
        if (string.IsNullOrWhiteSpace(RawResponseContent))
        {
            ResponseContent = string.Empty;
            return;
        }

        if (!IsPrettyFormat)
        {
            ResponseContent = RawResponseContent;
            return;
        }

        var type = GetResponseType();
        ResponseContentType = type;

        try
        {
            switch (type)
            {
                case "json":
                    using (var jsonDoc = JsonDocument.Parse(RawResponseContent))
                    {
                        ResponseContent = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });
                    }
                    break;

                case "xml":
                case "html":
                    try {
                        var doc = System.Xml.Linq.XDocument.Parse(RawResponseContent);
                        ResponseContent = doc.ToString();
                    } catch {
                        ResponseContent = RawResponseContent;
                    }
                    break;

                default:
                    ResponseContent = RawResponseContent;
                    break;
            }
        }
        catch
        {
            ResponseContent = RawResponseContent;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = (int)Math.Floor(Math.Log(bytes, 1024));
        order = Math.Min(order, sizes.Length - 1);
        double size = bytes / Math.Pow(1024, order);

        return $"{size:0.##} {sizes[order]}";
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
                Duration = Math.Max(0, metrics.DnsLookup),
                Color = TimelineColors.DnsLookup
            },
            new TimelineStageModel
            {
                Name = "TCP Handshake",
                Duration = Math.Max(0, metrics.TcpHandshake),
                Color = TimelineColors.TcpHandshake
            },
            new TimelineStageModel
            {
                Name = "SSL Handshake",
                Duration = Math.Max(0, metrics.SslHandshake),
                Color = TimelineColors.SslHandshake
            },
            new TimelineStageModel
            {
                Name = "Waiting (TTFB)",
                Duration = Math.Max(0, metrics.TimeToFirstByte),
                Color = TimelineColors.TimeToFirstByte
            },
            new TimelineStageModel
            {
                Name = "Content Download",
                Duration = Math.Max(0, metrics.ContentDownload),
                Color = TimelineColors.ContentDownload
            }
        };

        foreach (var stage in stages)
        {
            stage.DurationText = $"{stage.Duration:F2} ms";
            stage.WidthPercent = totalTime > 0 ? (stage.Duration / totalTime) * 100 : 0;
            TimelineStages.Add(stage);
        }
    }

    private void MarkAsDirty()
    {
        if (_isLoadingData || _activeRequest == null || _requestSnapshot == null) return;
        
        SyncToActiveRequest(); 
        
        IsDirty = _dirtyTracker.IsDirty(_activeRequest, _requestSnapshot);
    }

    private void SyncToActiveRequest()
    {
        if (_activeRequest == null) return;
        
        _activeRequest.Name = Name;
        _activeRequest.Url = RequestUrl;
        _activeRequest.MethodString = SelectedMethod;
        _activeRequest.Body = RequestBody;
        
        SyncCollection(_activeRequest.Headers, HeadersViewModel.Headers);
        SyncCollection(_activeRequest.Cookies, CookiesViewModel.Cookies);
        SyncCollection(_activeRequest.QueryParameters, QueryParamsViewModel.Parameters);

        _activeRequest.AuthData = AuthViewModel.ToAuthData();
    }
    
    private void SyncCollection(ObservableCollection<KeyValueItemModel> target, ObservableCollection<KeyValueItemModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(new KeyValueItemModel
            {
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            });
        }
    }
    
    private void UnsubscribeFromCollection(ObservableCollection<KeyValueItemModel> collection)
    {
        foreach (var item in collection)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    // ========================================
    // Dispose
    // ========================================
    
    public void Dispose()
    {
        UnsubscribeFromEvents();
        
        HeadersViewModel?.Dispose();
        QueryParamsViewModel?.Dispose();
        CookiesViewModel?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
