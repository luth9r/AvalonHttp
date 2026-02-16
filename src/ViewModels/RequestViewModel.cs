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
using System.Web;
using AvalonHttp.Messages;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents the view model for managing and handling the state of HTTP requests,
/// including their parameters, headers, authentication, cookies, and associated environments.
/// </summary>
public partial class RequestViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Defines a collection of static brushes representing different status colors used throughout
    /// the application for visual feedback, such as indicating success, warnings, errors, or readiness.
    /// </summary>
    private static class StatusColors
    {
        public static readonly IBrush Ready = new SolidColorBrush(Color.Parse("#6B7280"));
        public static readonly IBrush Success = new SolidColorBrush(Color.Parse("#10B981"));
        public static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F59E0B"));
        public static readonly IBrush Error = new SolidColorBrush(Color.Parse("#EF4444"));
    }

    /// <summary>
    /// Provides a set of predefined colors, represented as hexadecimal color codes,
    /// intended for illustrating various stages of a timeline in the application's
    /// operations or processes.
    /// </summary>
    private static class TimelineColors
    {
        public const string DnsLookup = "#10B981";
        public const string TcpHandshake = "#3B82F6";
        public const string SslHandshake = "#F59E0B";
        public const string TimeToFirstByte = "#EF4444";
        public const string ContentDownload = "#06B6D4";
    }

    /// <summary>
    /// Provides a read-only collection of commonly used HTTP methods for constructing HTTP requests.
    /// </summary>
    public static ObservableCollection<string> HttpMethods { get; } = new()
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    /// <summary>
    /// Encapsulates functionality to send HTTP requests and process their responses.
    /// </summary>
    private readonly IHttpService _httpService;

    /// <summary>
    /// Holds a reference to the current API request being processed or edited within the application.
    /// </summary>
    private ApiRequest? _activeRequest;

    /// <summary>
    /// A readonly instance of the service responsible for tracking changes to the state of an object,
    /// typically used to determine if a data model has been modified since its initial load or snapshot.
    /// </summary>
    private readonly IDirtyTrackerService _dirtyTracker;

    /// <summary>
    /// Stores a serialized snapshot of the current active API request's state
    /// to allow for changes to be reverted to this baseline if needed.
    /// </summary>
    private string? _requestSnapshot;

    /// <summary>
    /// Indicates whether the application is currently in the process of loading data,
    /// such as from a disk or network, and prevents actions that interfere with the load operation.
    /// </summary>
    private bool _isLoadingData;

    /// <summary>
    /// Indicates whether the URL query parameters are currently being synchronized with the active request.
    /// </summary>
    private bool _isSyncingUrl;

    /// <summary>
    /// Represents the currently selected tab in the request section of the application.
    /// </summary>
    [ObservableProperty]
    private string _selectedRequestTab = "Params";

    /// <summary>
    /// Represents the currently selected tab in the response section of the application.
    /// </summary>
    [ObservableProperty]
    private string _selectedResponseTab = "Body";

    /// <summary>
    /// Represents the name of the HTTP request, typically used as a label or identifier for better organization and display purposes.
    /// </summary>
    [ObservableProperty]
    private string _name = "New Request";

    /// <summary>
    /// Stores the URL associated with the HTTP request being prepared or executed.
    /// </summary>
    [ObservableProperty]
    private string _requestUrl = string.Empty;

    /// <summary>
    /// Represents the selected HTTP method for the request.
    /// </summary>
    [ObservableProperty]
    private string _selectedMethod = "GET";

    /// <summary>
    /// Stores the body content of the HTTP request to be sent.
    /// </summary>
    [ObservableProperty]
    private string _requestBody = string.Empty;

    /// <summary>
    /// Stores the response content of an HTTP request as a string.
    /// </summary>
    [ObservableProperty]
    private string _responseContent = string.Empty;

    /// <summary>
    /// Stores the raw content of the HTTP response as a plain text string.
    /// </summary>
    [ObservableProperty]
    private string _rawResponseContent = string.Empty;

    /// <summary>
    /// Holds the current status code or state of the HTTP request processing.
    /// </summary>
    [ObservableProperty]
    private string _statusCode = "Ready";

    /// <summary>
    /// Represents the duration of the HTTP request execution, measured in milliseconds.
    /// </summary>
    [ObservableProperty]
    private string _responseTime = "--";

    /// <summary>
    /// Stores the size of the response content, represented as a string, typically indicating the number of bytes received.
    /// </summary>
    [ObservableProperty]
    private string _responseSize = "--";

    /// <summary>
    /// Represents a field that indicates whether an operation, such as sending a network request,
    /// is currently in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Represents the content type of the HTTP response expected from the server.
    /// </summary>
    [ObservableProperty]
    private string _responseContentType = "json";

    /// <summary>
    /// Represents a value indicating whether the response content should be formatted using pretty formatting.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrettyActive))]
    [NotifyPropertyChangedFor(nameof(IsRawActive))]
    private bool _isPrettyFormat = true;

    /// <summary>
    /// Indicates whether there are unsaved changes in the current state of the application.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCurrentRequestCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    private bool _isDirty;

    /// <summary>
    /// Represents the brush used to indicate the current status of the HTTP request in the UI.
    /// </summary>
    [ObservableProperty]
    private IBrush _statusBrush = StatusColors.Ready;

    /// <summary>
    /// Represents a collection of headers included in the HTTP response for the current request.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResponseHeadersCount))]
    private ObservableCollection<ResponseHeaderModel> _responseHeaders = new();

    /// <summary>
    /// Represents a collection of cookies received as part of the current HTTP response.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResponseCookiesCount))]
    private ObservableCollection<ResponseHeaderModel> _responseCookies = new();

    /// <summary>
    /// Represents a collection of timeline stages associated with the current HTTP request and response lifecycle.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TimelineStageModel> _timelineStages = new();

    /// <summary>
    /// Represents the total time, in milliseconds, taken to complete the execution of the current HTTP request.
    /// </summary>
    [ObservableProperty]
    private double _totalRequestTime;

    /// <summary>
    /// Indicates whether the current HTTP request's response contains any data.
    /// </summary>
    [ObservableProperty]
    private bool _hasResponseData;

    /// <summary>
    /// Gets the total number of response headers currently stored in the response headers collection.
    /// </summary>
    public int ResponseHeadersCount => ResponseHeaders.Count;

    /// <summary>
    /// Gets the number of cookies present in the response.
    /// </summary>
    public int ResponseCookiesCount => ResponseCookies.Count;

    /// <summary>
    /// Indicates whether the "Pretty" format option is currently active.
    /// </summary>
    public bool IsPrettyActive => IsPrettyFormat;

    /// <summary>
    /// Indicates whether the raw response format is currently active.
    /// </summary>
    public bool IsRawActive => !IsPrettyFormat;

    /// <summary>
    /// Represents the view model for managing the HTTP headers within a request.
    /// </summary>
    public HeadersViewModel HeadersViewModel { get; }

    /// <summary>
    /// Provides functionality for managing and interacting with the query parameters of an HTTP request.
    /// </summary>
    public QueryParamsViewModel QueryParamsViewModel { get; }

    /// <summary>
    /// Represents the authentication-related view model used for managing authentication configurations in HTTP requests.
    /// </summary>
    public AuthViewModel AuthViewModel { get; }

    /// <summary>
    /// Represents the view model for managing cookies within an HTTP request context.
    /// </summary>
    public CookiesViewModel CookiesViewModel { get; }

    /// <summary>
    /// Represents the view model responsible for managing environments and associated environment variables.
    /// </summary>
    public EnvironmentsViewModel EnvironmentsViewModel { get; }

    /// <summary>
    /// Represents the ViewModel responsible for managing HTTP request data, including headers,
    /// query parameters, authentication, and cookies. Facilitates interaction with the associated services
    /// and ensures state updates in response to user actions or changes in the environment.
    /// </summary>
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


    /// <summary>
    /// Registers event handlers for various dependent ViewModel components to synchronize updates,
    /// ensuring proper handling of changes in query parameters, authentication states, headers, and cookies.
    /// </summary>
    private void SubscribeToEvents()
    {
        QueryParamsViewModel.UrlChanged += OnQueryParamsUrlChanged;
        AuthViewModel.PropertyChanged += OnAuthPropertyChanged;

        HeadersViewModel.Headers.CollectionChanged += OnHeadersCollectionChanged;
        QueryParamsViewModel.Parameters.CollectionChanged += OnParamsCollectionChanged;
        CookiesViewModel.Cookies.CollectionChanged += OnCookiesCollectionChanged;
    }

    /// <summary>
    /// Unsubscribes from various events from dependent ViewModel components to disable coordinated updates
    /// and to stop listening for changes in headers, query parameters, authentication, and cookies.
    /// </summary>
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
    
    #region Events

    /// <summary>
    /// Handles property changes in the associated AuthViewModel and marks the request as dirty
    /// if any authentication-related property is updated.
    /// </summary>
    /// <param name="sender">The source of the event, typically the AuthViewModel instance.</param>
    /// <param name="e">An instance of PropertyChangedEventArgs containing information about the property that changed.</param>
    private void OnAuthPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkAsDirty();
    }

    /// <summary>
    /// Handles changes in the headers collection by reacting to events generated
    /// when the collection is modified (e.g., items are added, removed, or replaced).
    /// </summary>
    /// <param name="sender">The source of the event, typically the headers collection.</param>
    /// <param name="e">Event data containing information about the type of change that occurred in the collection.</param>
    private void OnHeadersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleCollectionChanged(e);
    }

    /// <summary>
    /// Handles changes in the query parameter collection to ensure updates are properly reflected
    /// in the request model and to trigger necessary downstream updates.
    /// </summary>
    /// <param name="sender">The source of the event, typically the query parameters collection.</param>
    /// <param name="e">An object containing details about the collection change event.</param>
    private void OnParamsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleCollectionChanged(e);
    }

    /// <summary>
    /// Handles changes in the cookies collection by responding to
    /// notifications such as additions, removals, or updates to ensure
    /// the state of the application reflects the current cookies data.
    /// </summary>
    /// <param name="sender">
    /// The source of the event, typically the cookies collection that was modified.
    /// </param>
    /// <param name="e">
    /// Event data containing information about the changes to the cookies collection.
    /// </param>
    private void OnCookiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleCollectionChanged(e);
    }

    /// <summary>
    /// Handles changes in a collection by updating property change listeners on added or removed items
    /// and marking the state as dirty to reflect updates.
    /// </summary>
    /// <param name="e">Event data describing the changes in the collection, including added and removed items.</param>
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

    /// <summary>
    /// Handles the <see cref="ObservableObject.PropertyChanged"/> event of an item within the collection,
    /// marking the parent view model as dirty whenever a property of the item changes.
    /// </summary>
    /// <param name="sender">The object that raised the event. Represents the item whose property changed.</param>
    /// <param name="e">Event data containing the name of the property that changed.</param>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkAsDirty();
    }

    /// <summary>
    /// Handles property change notifications for the active API request.
    /// Updates related fields in the ViewModel and marks the request as dirty when
    /// specific properties, such as the request name, are modified.
    /// </summary>
    /// <param name="sender">The object that triggered the property change event.</param>
    /// <param name="e">Details of the changed property, including its name.</param>
    private void OnActiveRequestPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApiRequest.Name))
        {
            Name = _activeRequest!.Name;
            MarkAsDirty();
        }
    }

    /// <summary>
    /// Handles the event when the query parameters result in a change to the URL.
    /// Updates the URL to reflect the current state of the query parameters and marks the request as modified.
    /// Prevents recursive updates by managing internal synchronization states.
    /// </summary>
    /// <param name="sender">The source of the event triggering the URL change, likely the QueryParamsViewModel.</param>
    /// <param name="e">The new URL value derived from the updated query parameters.</param>
    private void OnQueryParamsUrlChanged(object? sender, string? e)
    {
        if (_isLoadingData || _isSyncingUrl)
        {
            return;
        }

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

    /// <summary>
    /// Handles changes to the Name property and marks the current state of the ViewModel as modified.
    /// This ensures that any updates to the Name property are tracked accurately for potential persistence or UI updates.
    /// </summary>
    /// <param name="value">The new value of the Name property that triggered the change.</param>
    partial void OnNameChanged(string value) => MarkAsDirty();

    /// <summary>
    /// Handles the logic to execute when the selected HTTP method changes.
    /// This method is invoked when the value of the associated property changes,
    /// ensuring that dependent components are updated accordingly.
    /// </summary>
    /// <param name="value">The new HTTP method selected, represented as a string.</param>
    partial void OnSelectedMethodChanged(string value) => MarkAsDirty();

    /// <summary>
    /// Invoked whenever the value of the request body is changed to trigger necessary updates
    /// and mark the request as modified.
    /// </summary>
    /// <param name="value">The new value of the request body.</param>
    partial void OnRequestBodyChanged(string value) => MarkAsDirty();

    /// <summary>
    /// Handles updates when the request URL changes. Synchronizes the query parameters from the URL,
    /// clears outdated response data, and marks the request as modified.
    /// </summary>
    /// <param name="value">The updated request URL.</param>
    partial void OnRequestUrlChanged(string value)
    {
        if (_isSyncingUrl || _isLoadingData)
        {
            return;
        }

        QueryParamsViewModel.LoadFromUrl(value);
        ClearResponseData();
        MarkAsDirty();
    }
    
    #endregion

    /// <summary>
    /// Updates the currently selected request tab to the specified tab name.
    /// </summary>
    /// <param name="tabName">The name of the tab to select, or null to default to the "Params" tab.</param>
    [RelayCommand]
    private void SelectRequestTab(string? tabName) => SelectedRequestTab = tabName ?? "Params";

    /// <summary>
    /// Updates the currently selected response tab in the interface to the specified tab name.
    /// </summary>
    /// <param name="tabName">The name of the tab to select, or null to default to the "Body" tab.</param>
    [RelayCommand]
    private void SelectResponseTab(string? tabName) => SelectedResponseTab = tabName ?? "Body";

    /// <summary>
    /// Sends an HTTP request based on the current configuration, including headers, query parameters,
    /// authentication, and cookies. Handles request validation, execution, and processing of the response,
    /// including error handling for common network and task-related exceptions.
    /// </summary>
    [RelayCommand]
    private async Task SendRequest()
    {
        if (!ValidateRequest())
        {
            return;
        }

        try
        {
            PrepareForRequest();
            
            var startTime = DateTime.Now;
            var now = DateTime.Now;
            var response = await ExecuteHttpRequestAsync();
            var duration = (now - startTime).TotalMilliseconds;

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

    /// <summary>
    /// Activates the "Pretty" format for the response content and ensures the relevant formatting changes are applied.
    /// </summary>
    [RelayCommand]
    private void SetPrettyFormat()
    {
        IsPrettyFormat = true;
        ApplyFormat();
    }

    /// <summary>
    /// Activates the "Raw" format view for the response content, disabling the "Pretty" format
    /// and applying the appropriate formatting changes.
    /// </summary>
    [RelayCommand]
    private void SetRawFormat()
    {
        IsPrettyFormat = false;
        ApplyFormat();
    }

    /// <summary>
    /// Saves the current request by persisting its data, clearing the dirty state,
    /// and broadcasting a notification to indicate the request has been successfully saved.
    /// The method only executes if the active request is set and contains unsaved changes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveRequest))]
    private void SaveCurrentRequest()
    {
        if (_activeRequest == null)
        {
            return;
        }

        SaveRequestData();
        _requestSnapshot = _dirtyTracker.TakeSnapshot(_activeRequest);
        IsDirty = false;
        
        WeakReferenceMessenger.Default.Send(new RequestSavedMessage(_activeRequest));
    }

    /// <summary>
    /// Determines whether the current request can be saved based on the modification state
    /// and the existence of an active request.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether the current request is eligible to be saved.
    /// Returns true if there are unsaved changes and an active request exists; otherwise, false.
    /// </returns>
    private bool CanSaveRequest() => IsDirty && _activeRequest != null;

    /// <summary>
    /// Reverts the current changes made to the active request by restoring its
    /// previous state from a saved snapshot, if available. Ensures the request
    /// is reset to its original values prior to modifications.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void RevertChanges()
    {
        if (_activeRequest == null || string.IsNullOrEmpty(_requestSnapshot))
        {
            return;
        }

        try
        {
            _isLoadingData = true;
            
            var originalState = JsonSerializer.Deserialize<ApiRequest>(_requestSnapshot);

            if (originalState != null)
            {
                Name = originalState.Name;
                RequestUrl = originalState.Url;
                SelectedMethod = originalState.MethodString;
                RequestBody = originalState.Body;
                
                LoadCollection(HeadersViewModel.Headers, originalState.Headers);
                LoadCollection(CookiesViewModel.Cookies, originalState.Cookies);
                
                QueryParamsViewModel.LoadFromUrl(originalState.Url);
                
                AuthViewModel.LoadFromAuthData(originalState.AuthData);
                
                SyncToActiveRequest();
                
                IsDirty = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to revert changes: {ex.Message}");
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    /// <summary>
    /// Determines whether changes can be reverted based on the current state of the view model.
    /// This method returns true if there are unsaved changes and an active request is present.
    /// </summary>
    /// <returns>
    /// A boolean value indicating whether the revert operation is allowed.
    /// Returns true if the view model has unsaved changes and there is an active request; otherwise, false.
    /// </returns>
    private bool CanRevert() => IsDirty && _activeRequest != null;

    // ========================================
    // HTTP Request Methods
    // ========================================

    /// <summary>
    /// Validates the current request to ensure it meets basic requirements, such as checking for a non-empty URL.
    /// Updates the status code and corresponding visual state if validation fails.
    /// </summary>
    /// <returns>
    /// True if the request is valid; otherwise, false.
    /// </returns>
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

    /// <summary>
    /// Prepares the view model for a new HTTP request by resetting relevant data and
    /// updating the UI state to indicate that a request is in progress.
    /// Resets response-related collections, clears previous response data, and updates
    /// the status indicators to show the "sending" state.
    /// </summary>
    private void PrepareForRequest()
    {
        IsLoading = true;
        HasResponseData = false;
        ClearResponseCollections();
        
        StatusCode = "Sending...";
        StatusBrush = StatusColors.Warning;
    }

    /// <summary>
    /// Clears all response-related collections and resets associated properties to their default states.
    /// This method is used to prepare the ViewModel for a new request by removing any residual data
    /// from previous responses, including content, headers, cookies, timeline stages, and request timing.
    /// </summary>
    private void ClearResponseCollections()
    {
        ResponseContent = string.Empty;
        RawResponseContent = string.Empty;
        ResponseHeaders.Clear();
        ResponseCookies.Clear();
        TimelineStages.Clear();
        TotalRequestTime = 0;
    }

    /// <summary>
    /// Executes an HTTP request asynchronously using the specified URL, HTTP method, headers, and body,
    /// as configured in the associated RequestViewModel. The HTTP request is sent through the
    /// underlying implementation of the <see cref="IHttpService"/> interface, which handles communication
    /// with the target server and returns the response.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an <see cref="HttpResponseMessage"/>
    /// instance representing the HTTP response received from the server.
    /// </returns>
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

    /// <summary>
    /// Constructs and returns a list of headers to be included in an HTTP request.
    /// This includes custom headers, cookie headers, and authentication headers.
    /// </summary>
    /// <returns>
    /// A list of key-value pairs representing the headers for the HTTP request.
    /// </returns>
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

    /// <summary>
    /// Constructs the final URL for an HTTP request by resolving variables within
    /// the base request URL and appending any required authentication query parameters.
    /// </summary>
    /// <returns>
    /// The finalized URL string to be used for the HTTP request, with variable
    /// placeholders resolved and necessary query parameters appended.
    /// </returns>
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

    /// <summary>
    /// Appends the specified query parameters to the provided URL.
    /// </summary>
    /// <param name="url">The base URL to which query parameters will be added.</param>
    /// <param name="queryParams">A dictionary containing the query parameters to append, where keys are parameter names and values are their corresponding values.</param>
    /// <returns>A string representing the URL with the appended query parameters. If an exception occurs, the original URL is returned.</returns>
    private static string AppendQueryParams(string url, Dictionary<string, string> queryParams)
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

    /// <summary>
    /// Processes the HTTP response received from a request, updating the response details
    /// and associated ViewModel properties to reflect the status, content, headers,
    /// and cookies retrieved, as well as the elapsed processing duration.
    /// </summary>
    /// <param name="response">The HTTP response message obtained from the completed request.</param>
    /// <param name="duration">The total duration, in milliseconds, taken to process the request and obtain a response.</param>
    /// <returns>A Task that represents the asynchronous operation of processing the response.</returns>
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

    /// <summary>
    /// Updates the status display to reflect the HTTP status code and status text,
    /// and assigns an appropriate color brush to indicate the status category (success, warning, or error).
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the response.</param>
    /// <param name="statusText">The textual description of the HTTP status code.</param>
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

    /// <summary>
    /// Processes the HTTP response content by calculating its size, formatting it as raw data,
    /// and updating related response properties within the view model.
    /// </summary>
    /// <param name="content">The string content of the HTTP response to be processed.</param>
    private void ProcessResponseContent(string content)
    {
        var contentLength = Encoding.UTF8.GetByteCount(content);
        ResponseSize = FormatBytes(contentLength);
        RawResponseContent = content;
    }

    /// <summary>
    /// Populates the collection of response headers by extracting them from the given HTTP response.
    /// This includes both general headers and content-specific headers from the response.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponseMessage"/> containing the headers to process and load.</param>
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

    /// <summary>
    /// Extracts and loads cookies from the provided HTTP response object, including cookies
    /// found in the response headers and content headers. Parses cookie values and updates
    /// the ResponseCookies collection, ensuring the response-related cookie count is updated.
    /// </summary>
    /// <param name="response">
    /// The HTTP response object from which cookies will be extracted and processed.
    /// </param>
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

    /// <summary>
    /// Parses a collection of cookie strings and extracts their names and values, populating
    /// the ResponseCookies collection with the parsed data.
    /// </summary>
    /// <param name="cookieValues">A collection of cookie strings to be parsed, where each cookie string
    /// is expected to include a name-value pair separated by '=' and optionally followed by additional metadata.</param>
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

    /// <summary>
    /// Handles cleanup and UI updates when a request is cancelled by the user or the system.
    /// Updates the response content, status code, and associated UI properties to reflect the cancellation status.
    /// </summary>
    private void HandleRequestCancelled()
    {
        StatusCode = "Cancelled";
        RawResponseContent = "Request was cancelled";
        ResponseContent = RawResponseContent;
        StatusBrush = StatusColors.Warning;
        HasResponseData = false;
    }

    /// <summary>
    /// Handles network-related errors by updating response properties and status indicators
    /// to reflect the occurrence of a network error.
    /// </summary>
    /// <param name="ex">The <see cref="HttpRequestException"/> instance representing the network error that occurred.</param>
    private void HandleNetworkError(HttpRequestException ex)
    {
        StatusCode = "Network Error";
        RawResponseContent = $"Network error: {ex.Message}";
        ResponseContent = RawResponseContent;
        StatusBrush = StatusColors.Error;
        HasResponseData = false;
    }

    /// <summary>
    /// Handles general errors encountered during the execution of HTTP requests by updating
    /// the UI with error information, setting the status code, response content, and UI styling
    /// to indicate the presence of an error.
    /// </summary>
    /// <param name="ex">The exception that represents the encountered error, including its message and details.</param>
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
    /// Loads data from the specified API request into the ViewModel, setting up necessary state,
    /// event subscriptions, and internal properties for request handling. This method also clears
    /// previous response data and initializes a snapshot for tracking changes.
    /// </summary>
    /// <param name="request">The API request instance containing the data to be loaded.</param>
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

    /// <summary>
    /// Unsubscribes from the property-changed notifications of the currently active API request
    /// to prevent memory leaks and unintended behavior when the request is no longer in use.
    /// </summary>
    private void UnsubscribeFromActiveRequest()
    {
        if (_activeRequest != null)
        {
            _activeRequest.PropertyChanged -= OnActiveRequestPropertyChanged;
        }
    }

    /// <summary>
    /// Loads the basic properties of the provided API request object into the current view model.
    /// </summary>
    /// <param name="request">The API request containing the properties to load.</param>
    private void LoadBasicProperties(ApiRequest request)
    {
        Name = request.Name;
        RequestUrl = request.Url;
        SelectedMethod = request.MethodString;
        RequestBody = request.Body;
    }

    /// <summary>
    /// Initializes and populates the necessary collections such as headers, cookies, and query parameters
    /// based on the data provided in the given <see cref="ApiRequest"/> object.
    /// </summary>
    /// <param name="request">
    /// The <see cref="ApiRequest"/> object containing the data to load into the respective collections.
    /// </param>
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

    /// <summary>
    /// Replaces the contents of the target collection with items from the source collection,
    /// while ensuring that each item is properly transformed into a new instance. This method
    /// effectively synchronizes the target collection with the source, maintaining separate
    /// object references for each item.
    /// </summary>
    /// <param name="target">The target collection to be updated. It will be cleared and repopulated with items from the source collection.</param>
    /// <param name="source">The source collection containing the items to copy into the target collection.</param>
    private void LoadCollection(
        ObservableCollection<KeyValueItemModel> target,
        ObservableCollection<KeyValueItemModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item.Key) ||
                string.IsNullOrWhiteSpace(item.Value))
                continue;
            
            target.Add(new KeyValueItemModel
            {
                Key = item.Key,
                Value = item.Value,
                IsEnabled = item.IsEnabled
            });
        }
    }

    /// <summary>
    /// Attaches event handlers to each item in the given collection to monitor property changes.
    /// This ensures that updates to individual items within the collection are detected and
    /// can trigger appropriate responses or further updates.
    /// </summary>
    /// <param name="collection">The collection of key-value item models to which event subscriptions are added.</param>
    private void SubscribeToCollection(ObservableCollection<KeyValueItemModel> collection)
    {
        foreach (var item in collection)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    /// <summary>
    /// Loads authentication data into the associated <see cref="AuthViewModel"/>.
    /// </summary>
    /// <param name="authData">The authentication data to be loaded into the ViewModel. Can be null.</param>
    private void LoadAuthData(AuthData? authData)
    {
        AuthViewModel.LoadFromAuthData(authData);
    }

    /// <summary>
    /// Saves the current state of the active API request, updating its name, URL, body, headers,
    /// query parameters, cookies, and authentication data based on the current ViewModel data.
    /// This method ensures that changes made in the ViewModel are reflected in the underlying request object.
    /// </summary>
    private void SaveRequestData()
    {
        if (_activeRequest == null)
        {
            return;
        }

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
        
        LoadCollection(_activeRequest.Headers, HeadersViewModel.Headers);
        LoadCollection(_activeRequest.QueryParameters, QueryParamsViewModel.Parameters);
        LoadCollection(_activeRequest.Cookies, CookiesViewModel.Cookies);
        
        _activeRequest.AuthData = AuthViewModel.ToAuthData();
        
        System.Diagnostics.Debug.WriteLine($"🟢 SaveRequestData END");
    }

    /// <summary>
    /// Saves the current HTTP response to a file, providing functionality to persist
    /// response data for later analysis or use. Handles potential exceptions during
    /// the file-write process and logs failure details if encountered.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous save operation.
    /// </returns>
    [RelayCommand]
    private async Task SaveResponse()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.StorageProvider == null)
                {
                    return;
                }

                var fileTypeFilter = new Avalonia.Platform.Storage.FilePickerFileType("JSON File")
                {
                    Patterns = new[] { "*.json" },
                    MimeTypes = new[] { "application/json" }
                };

                var safeFileName = string.Join("_", (Name).Split(System.IO.Path.GetInvalidFileNameChars()));
            
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

    /// <summary>
    /// Copies the response content to the system clipboard, enabling users to quickly retrieve and use
    /// the response data outside the application. Logs an error message in case of failure.
    /// </summary>
    /// <returns>A task representing the asynchronous copy operation.</returns>
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

    /// <summary>
    /// Determines the type of response content based on the "Content-Type" header in the response.
    /// Possible return values include "json" for JSON content, "xml" for XML content, "html" for HTML content,
    /// and "text" for all other types of content.
    /// </summary>
    /// <returns>
    /// A string representing the inferred response content type: "json", "xml", "html", or "text".
    /// </returns>
    private string GetResponseType()
    {
        var contentType = ResponseHeaders.FirstOrDefault(h => 
            h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value ?? "";

        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (contentType.Contains("application/xml", StringComparison.OrdinalIgnoreCase) || 
            contentType.Contains("text/xml", StringComparison.OrdinalIgnoreCase))
        {
            return "xml";
        }

        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return "html";
        }

        return "text";
    }

    /// <summary>
    /// Replaces variable placeholders in the given text with their corresponding values from the current environment.
    /// </summary>
    /// <param name="text">The text containing variable placeholders to be resolved.</param>
    /// <returns>A string where all variable placeholders have been replaced with their resolved values.</returns>
    private string ResolveVariables(string text)
    {
        return EnvironmentsViewModel.ResolveVariables(text);
    }

    /// <summary>
    /// Updates the RequestUrl property by constructing a new URL based on the current query parameters.
    /// This method combines the base URL with the updated query parameters and ensures proper formatting and decoding
    /// for better readability, such as replacing encoded brackets with their plain-text equivalents.
    /// </summary>
    private void UpdateUrlFromQueryParams()
    {
        var currentUrl = RequestUrl;
        var baseUrl = currentUrl.Split('?')[0];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        var newUrl = QueryParamsViewModel.BuildUrl(baseUrl);
    
        // Decode brackets for readability: %7B%7BVar%7D%7D → {{Var}}
        newUrl = newUrl.Replace("%7B", "{").Replace("%7D", "}");

        if (RequestUrl != newUrl)
        {
            RequestUrl = newUrl;
        }
    }

    /// <summary>
    /// Clears all data related to the HTTP response, resetting the associated properties and collections
    /// to their default states. This method ensures that outdated or irrelevant response data is discarded
    /// in preparation for new request operations.
    /// </summary>
    private void ClearResponseData()
    {
        if (!HasResponseData)
        {
            return;
        }

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

    /// <summary>
    /// Applies formatting to the raw response content based on the selected format preference.
    /// If the format is set to "pretty", attempts to parse and format the response content
    /// according to its detected type (e.g., JSON, XML). Otherwise, returns the raw response content as is.
    /// </summary>
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

    /// <summary>
    /// Converts the given byte size into a human-readable format, such as KB, MB, GB, etc.,
    /// based on the magnitude of the byte count.
    /// </summary>
    /// <param name="bytes">The size in bytes to be converted into a readable format.</param>
    /// <returns>A string representing the byte size in a human-readable format, including
    /// the appropriate unit (e.g., "B", "KB", "MB").</returns>
    private static string FormatBytes(long bytes)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = (int)Math.Floor(Math.Log(bytes, 1024));
        order = Math.Min(order, sizes.Length - 1);
        double size = bytes / Math.Pow(1024, order);

        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Constructs and populates the timeline visualization by processing various request stages,
    /// including DNS lookup, TCP handshake, SSL handshake, time to first byte (TTFB), and content download.
    /// Each stage is assigned a duration, color, and percentage width proportional to the total request time.
    /// </summary>
    /// <param name="totalTime">The total time of the HTTP request in milliseconds.</param>
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

    /// <summary>
    /// Marks the current state of the active request as "dirty" if changes have occurred,
    /// reflecting that the request has been modified since the last synchronized state.
    /// The method uses the provided dirty tracker service to compare the active request
    /// against its snapshot and updates the "IsDirty" status accordingly.
    /// Does nothing if data is being loaded or if required components are null.
    /// </summary>
    private void MarkAsDirty()
    {
        if (_isLoadingData || _activeRequest == null || _requestSnapshot == null)
        {
            return;
        }

        SyncToActiveRequest(); 
        
        IsDirty = _dirtyTracker.IsDirty(_activeRequest, _requestSnapshot);
    }

    /// <summary>
    /// Synchronizes the current state of the request properties, including URL, headers, cookies,
    /// query parameters, authentication data, and other related fields, with the active request model.
    /// Ensures that the most recent changes in the ViewModel are reflected in the backing ApiRequest object.
    /// </summary>
    private void SyncToActiveRequest()
    {
        if (_activeRequest == null)
        {
            return;
        }

        _activeRequest.Name = Name;
        _activeRequest.Url = RequestUrl;
        _activeRequest.MethodString = SelectedMethod;
        _activeRequest.Body = RequestBody;
        
        SyncCollection(_activeRequest.Headers, HeadersViewModel.Headers);
        SyncCollection(_activeRequest.Cookies, CookiesViewModel.Cookies);
        SyncCollection(_activeRequest.QueryParameters, QueryParamsViewModel.Parameters);

        _activeRequest.AuthData = AuthViewModel.ToAuthData();
    }

    /// <summary>
    /// Synchronizes the contents of a target collection with a source collection by clearing the target
    /// and adding items from the source. Each item is copied into a new instance to avoid shared references.
    /// </summary>
    /// <param name="target">The target collection to be updated with items from the source.</param>
    /// <param name="source">The source collection providing the items to synchronize with the target.</param>
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

    /// <summary>
    /// Detaches all subscribed event handlers from the PropertyChanged event for each item
    /// within the specified observable collection, ensuring proper cleanup and prevention
    /// of memory leaks or unintended side effects.
    /// </summary>
    /// <param name="collection">
    /// The observable collection of KeyValueItemModel objects from which to unsubscribe
    /// the PropertyChanged event handlers.
    /// </param>
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

    /// <summary>
    /// Releases resources used by the RequestViewModel object, including disposing of any dependent
    /// view models and unsubscribing from events to prevent memory leaks and ensure proper cleanup.
    /// </summary>
    public void Dispose()
    {
        UnsubscribeFromEvents();
        
        HeadersViewModel.Dispose();
        QueryParamsViewModel.Dispose();
        CookiesViewModel.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
