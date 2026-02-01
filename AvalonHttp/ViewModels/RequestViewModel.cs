using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

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
    // Fields
    // ========================================
    
    private readonly IHttpService _httpService;
    private ApiRequest? _activeRequest;
    private bool _isLoadingData;
    private bool _isSyncingUrl;

    // ========================================
    // Observable Properties
    // ========================================
    
    [ObservableProperty]
    private string _name = "No Request";

    [ObservableProperty]
    private string _requestUrl = "";

    [ObservableProperty]
    private string _requestName = "New Request";

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
    [NotifyPropertyChangedFor(nameof(IsPrettyActive))]
    [NotifyPropertyChangedFor(nameof(IsRawActive))]
    private bool _isPrettyFormat = true;

    [ObservableProperty]
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
        CookiesViewModel cookiesViewModel)
    {
        _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
        HeadersViewModel = headersViewModel ?? throw new ArgumentNullException(nameof(headersViewModel));
        QueryParamsViewModel = queryParamsViewModel ?? throw new ArgumentNullException(nameof(queryParamsViewModel));
        AuthViewModel = authViewModel ?? throw new ArgumentNullException(nameof(authViewModel));
        CookiesViewModel = cookiesViewModel ?? throw new ArgumentNullException(nameof(cookiesViewModel));

        SubscribeToEvents();
    }

    // ========================================
    // Event Subscription
    // ========================================
    
    private void SubscribeToEvents()
    {
        // Child ViewModels
        QueryParamsViewModel.UrlChanged += OnQueryParamsUrlChanged;
        AuthViewModel.PropertyChanged += OnAuthPropertyChanged;
        
        // Collections
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
        UnsubscribeFromOldItems(e);
        SubscribeToNewItems(e);
        MarkAsDirty();
    }

    private void UnsubscribeFromOldItems(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }
    }

    private void SubscribeToNewItems(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (KeyValueItemModel item in e.NewItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged; // Prevent duplicates
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
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

    [RelayCommand]
    private void SaveCurrentRequest()
    {
        if (_activeRequest == null)
        {
            System.Diagnostics.Debug.WriteLine("❌ SaveCurrentRequest: _activeRequest is null!");
            return;
        }

        LogBeforeSave();
        SaveRequestData();
        LogAfterSave();

        RequestSaved?.Invoke(this, _activeRequest);
        IsDirty = false;
    }

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
        ResponseContent = "";
        RawResponseContent = "";
        ResponseHeaders.Clear();
        ResponseCookies.Clear();
        TimelineStages.Clear();
        TotalRequestTime = 0;
    }

    private async Task<HttpResponseMessage> ExecuteHttpRequestAsync()
    {
        var headersList = BuildHeadersList();
        var finalUrl = BuildFinalUrl();

        return await _httpService.SendRequestAsync(
            finalUrl,
            SelectedMethod,
            headersList,
            RequestBody);
    }

    private List<KeyValuePair<string, string>> BuildHeadersList()
    {
        var headersList = new List<KeyValuePair<string, string>>(HeadersViewModel.GetEnabledHeaders());

        // Add cookies
        var cookieHeaderValue = CookiesViewModel.GetCookieHeaderValue();
        if (!string.IsNullOrWhiteSpace(cookieHeaderValue))
        {
            headersList.Add(new KeyValuePair<string, string>("Cookie", cookieHeaderValue));
        }

        // Add auth headers
        var authHeaders = AuthViewModel.GetAuthHeaders();
        headersList.AddRange(authHeaders);

        return headersList;
    }

    private string BuildFinalUrl()
    {
        var finalUrl = RequestUrl;
        var authQueryParams = AuthViewModel.GetAuthQueryParams();
        
        if (authQueryParams.Count > 0)
        {
            try
            {
                var uriBuilder = new UriBuilder(RequestUrl);
                var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);

                foreach (var param in authQueryParams)
                {
                    query[param.Key] = param.Value;
                }

                uriBuilder.Query = query.ToString();
                finalUrl = uriBuilder.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add auth query params: {ex.Message}");
            }
        }

        return finalUrl;
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
    }

    private void LoadResponseCookies(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                var parts = cookie.Split(';')[0].Split('=', 2);
                if (parts.Length >= 2)
                {
                    ResponseCookies.Add(new ResponseHeaderModel
                    {
                        Name = parts[0].Trim(),
                        Value = parts[1].Trim()
                    });
                }
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
            
            _activeRequest = request;
            _activeRequest.PropertyChanged += OnActiveRequestPropertyChanged;

            LoadBasicProperties(request);
            LoadCollections(request);
            LoadAuthData(request.AuthData);

            ClearResponseData();
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
        RequestBody = request.Body ?? "";
    }

    private void LoadCollections(ApiRequest request)
    {
        LoadCollection(HeadersViewModel.Headers, request.Headers);
        LoadCollection(CookiesViewModel.Cookies, request.Cookies);
        
        QueryParamsViewModel.LoadFromUrl(request.Url);
        foreach (var param in QueryParamsViewModel.Parameters)
        {
            param.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void LoadCollection(
        ObservableCollection<KeyValueItemModel> target,
        ObservableCollection<KeyValueItemModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            var newItem = new KeyValueItemModel
            {
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            };
            newItem.PropertyChanged += OnItemPropertyChanged;
            target.Add(newItem);
        }
    }

    private void LoadAuthData(AuthData? authData)
    {
        if (authData != null)
        {
            AuthViewModel.SelectedAuthType = authData.Type;
            AuthViewModel.BasicUsername = authData.BasicUsername ?? "";
            AuthViewModel.BasicPassword = authData.BasicPassword ?? "";
            AuthViewModel.BearerToken = authData.BearerToken ?? "";
            AuthViewModel.ApiKeyName = authData.ApiKeyName ?? "";
            AuthViewModel.ApiKeyValue = authData.ApiKeyValue ?? "";
            AuthViewModel.ApiKeyLocation = authData.ApiKeyLocation;
        }
        else
        {
            AuthViewModel.Reset();
        }
    }

    private void SaveRequestData()
    {
        _activeRequest!.Name = Name;
        _activeRequest.Url = RequestUrl;
        _activeRequest.MethodString = SelectedMethod;
        _activeRequest.Body = RequestBody ?? "";

        SaveCollection(_activeRequest.Headers, HeadersViewModel.Headers);
        SaveCollection(_activeRequest.QueryParameters, QueryParamsViewModel.Parameters);
        SaveCollection(_activeRequest.Cookies, CookiesViewModel.Cookies);
        SaveAuthData();
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

    private void SaveAuthData()
    {
        if (_activeRequest!.AuthData == null)
        {
            _activeRequest.AuthData = new AuthData();
        }

        _activeRequest.AuthData.Type = AuthViewModel.SelectedAuthType;
        _activeRequest.AuthData.BasicUsername = AuthViewModel.BasicUsername ?? "";
        _activeRequest.AuthData.BasicPassword = AuthViewModel.BasicPassword ?? "";
        _activeRequest.AuthData.BearerToken = AuthViewModel.BearerToken ?? "";
        _activeRequest.AuthData.ApiKeyName = AuthViewModel.ApiKeyName ?? "";
        _activeRequest.AuthData.ApiKeyValue = AuthViewModel.ApiKeyValue ?? "";
        _activeRequest.AuthData.ApiKeyLocation = AuthViewModel.ApiKeyLocation;
    }

    // ========================================
    // Helper Methods
    // ========================================
    
    private void UpdateUrlFromQueryParams()
    {
        var currentUrl = RequestUrl ?? "";
        var baseUrl = currentUrl.Split('?')[0];

        if (string.IsNullOrWhiteSpace(baseUrl)) return;

        var newUrl = QueryParamsViewModel.BuildUrl(baseUrl);
        if (RequestUrl != newUrl)
        {
            RequestUrl = newUrl;
        }
    }

    private void ClearResponseData()
    {
        if (!HasResponseData) return;

        HasResponseData = false;
        ResponseContent = "";
        RawResponseContent = "";
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

        try
        {
            if (IsPrettyFormat)
            {
                var jsonDoc = JsonDocument.Parse(RawResponseContent);
                ResponseContent = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            else
            {
                ResponseContent = RawResponseContent;
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
                Duration = metrics.DnsLookup,
                Color = TimelineColors.DnsLookup
            },
            new TimelineStageModel
            {
                Name = "TCP Handshake",
                Duration = metrics.TcpHandshake,
                Color = TimelineColors.TcpHandshake
            },
            new TimelineStageModel
            {
                Name = "SSL Handshake",
                Duration = metrics.SslHandshake,
                Color = TimelineColors.SslHandshake
            },
            new TimelineStageModel
            {
                Name = "Waiting (TTFB)",
                Duration = metrics.TimeToFirstByte,
                Color = TimelineColors.TimeToFirstByte
            },
            new TimelineStageModel
            {
                Name = "Content Download",
                Duration = metrics.ContentDownload,
                Color = TimelineColors.ContentDownload
            }
        };

        foreach (var stage in stages)
        {
            stage.DurationText = $"{(stage.Duration > 0 ? stage.Duration : 0):F2} ms";
            stage.WidthPercent = totalTime > 0 ? (stage.Duration / totalTime) * 100 : 0;
            TimelineStages.Add(stage);
        }
    }

    private void MarkAsDirty()
    {
        if (_isLoadingData || _activeRequest == null) return;
        IsDirty = true;
    }

    private void UnsubscribeFromCollection(ObservableCollection<KeyValueItemModel> collection)
    {
        foreach (var item in collection)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    // ========================================
    // Logging
    // ========================================
    
    private void LogBeforeSave()
    {
        System.Diagnostics.Debug.WriteLine($"=== SAVE REQUEST START ===");
        System.Diagnostics.Debug.WriteLine($"📝 Source (ViewModel):");
        System.Diagnostics.Debug.WriteLine($"  RequestBody: '{RequestBody}' (length: {RequestBody?.Length ?? 0})");
        System.Diagnostics.Debug.WriteLine($"  Cookies: {CookiesViewModel.Cookies.Count}");
        System.Diagnostics.Debug.WriteLine($"  Auth: {AuthViewModel.SelectedAuthType}");
    }

    private void LogAfterSave()
    {
        System.Diagnostics.Debug.WriteLine($"📦 Target (Model) AFTER:");
        System.Diagnostics.Debug.WriteLine($"  Body: '{_activeRequest!.Body}' (length: {_activeRequest.Body?.Length ?? 0})");
        System.Diagnostics.Debug.WriteLine($"  Cookies: {_activeRequest.Cookies.Count}");
        System.Diagnostics.Debug.WriteLine($"  Auth: {_activeRequest.AuthData?.Type}");
        System.Diagnostics.Debug.WriteLine($"=== SAVE REQUEST END ===");
    }

    // ========================================
    // Dispose
    // ========================================
    
    public void Dispose()
    {
        UnsubscribeFromEvents();
        
        CookiesViewModel?.Dispose();
        QueryParamsViewModel?.Dispose();
    }
}
