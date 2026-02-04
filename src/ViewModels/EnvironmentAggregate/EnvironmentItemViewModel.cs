using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.ViewModels.EnvironmentAggregate;

public partial class EnvironmentItemViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Reference to parent view model
    /// </summary>
    private readonly EnvironmentsViewModel _parent;
    
    /// <summary>
    /// Reference to source model
    /// </summary>
    private readonly Environment _environment;

    /// <summary>
    /// Stores the original name from source model.
    /// </summary>
    private string _originalName = string.Empty;
    
    /// <summary>
    /// Stores the original variables JSON from source model.
    /// </summary>
    private string _originalVariablesJson = string.Empty;
    
    /// <summary>
    /// Stores the name before editing.
    /// </summary>
    private string _nameBeforeEdit = string.Empty;
    
    public Environment Environment => _environment;
    public Guid Id => _environment.Id;
    public bool IsGlobal => _environment.IsGlobal;

    /// <summary>
    /// The name of the environment item, used for display and identification.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name;

    /// <summary>
    /// Stores the JSON representation of variables for the environment.
    /// Updates dependent properties such as IsJsonValid, JsonErrorMessage, and VariablesCount
    /// when its value changes.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJsonValid))]
    [NotifyPropertyChangedFor(nameof(JsonErrorMessage))]
    [NotifyPropertyChangedFor(nameof(VariablesCount))]
    private string _variablesJson;

    /// <summary>
    /// Indicates whether the environment item is currently being edited.
    /// </summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// Indicates whether the environment item is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Indicates whether the environment item is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Indicates whether the JSON is valid.
    /// </summary>
    public bool IsJsonValid => TryParseJson(VariablesJson);
    
    /// <summary>
    /// Error message to display when JSON is invalid.
    /// </summary>
    public string JsonErrorMessage => IsJsonValid 
        ? string.Empty 
        : "Invalid JSON format";
    
    /// <summary>
    /// Number of variables in the JSON.
    /// </summary>
    public int VariablesCount
    {
        get
        {
            try
            {
                if (!IsJsonValid)
                {
                    return 0;
                }

                using var doc = JsonDocument.Parse(VariablesJson);
                return doc.RootElement.EnumerateObject().Count();
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Indicates whether the environment item has been modified since last save.
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;
    
    public EnvironmentItemViewModel(Environment environment, EnvironmentsViewModel parent)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));

        // Initialize from model (single direction)
        LoadFromModel();
    }

    /// <summary>
    /// Load data from source model "<see cref="Environment"/>."/>
    /// </summary>
    private void LoadFromModel()
    {
        // Set initial values from source model
        Name = _environment.Name;
        VariablesJson = _environment.VariablesJson;

        // Make initial snapshot
        UpdateSnapshot();
    }

    /// <summary>
    /// Updates the internal snapshot of the environment's current state. This method
    /// stores the current values of the environment's name and variables in the snapshot,
    /// applies the changes to the underlying model, and resets the "dirty" state to false.
    /// </summary>
    public void UpdateSnapshot()
    {
        // Make initial snapshot
        _originalName = Name;
        _originalVariablesJson = VariablesJson;
        
        // Apply changes to model (update model with current values)
        ApplyToModel();
        
        // Reset dirty state
        IsDirty = false;
    }

    /// <summary>
    /// Updates the associated environment model with the current values stored in the view model.
    /// The method synchronizes properties such as name and JSON variables,
    /// ensuring the model reflects the latest state of the view model instance.
    /// </summary>
    public void ApplyToModel()
    {
        _environment.Name = Name;
        _environment.VariablesJson = VariablesJson;
    }

    /// <summary>
    /// Initiates the renaming process for the current environment item.
    /// If the item is marked as a global environment, the operation is not allowed.
    /// Saves the current name for potential restoration.
    /// </summary>
    [RelayCommand]
    private void StartRename()
    {
        if (IsGlobal)
        {
            return; // Globals can't be renamed
        }

        // Save current name for potential restoration
        _nameBeforeEdit = Name;
        IsEditing = true;
    }

    /// <summary>
    /// Completes the renaming process for the associated environment item.
    /// If the renaming is invalid or cannot be completed, the rename operation is canceled.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFinishRename))]
    private void FinishRename()
    {
        if (!CanFinishRename())
        {
            CancelRename();
            return;
        }

        IsEditing = false;
    }

    /// <summary>
    /// Determines whether the renaming operation can be completed.
    /// </summary>
    private bool CanFinishRename()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        if (Name == _originalName)
        {
            return true; // No changes
        }

        // Check if name is unique (optional)
        return true;
    }

    /// <summary>
    /// Cancels the renaming operation for the associated environment item.
    /// </summary>
    [RelayCommand]
    private void CancelRename()
    {
        Name = _nameBeforeEdit;
        IsEditing = false;
    }

    /// <summary>
    /// Reverts any unsaved changes made to the environment's name and variables JSON
    /// by restoring their original values from the internal snapshot.
    /// </summary>
    /// <remarks>
    /// This method reassigns the environment's <see cref="Name"/> and <see cref="VariablesJson"/>
    /// properties to their original values if they have been modified. It ensures that the
    /// state of the environment matches the snapshot before any changes were applied.
    /// <para>
    /// Can only execute if changes are detected through the <see cref="IsDirty"/> property.
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void RevertChanges()
    {
        
        if (!string.Equals(Name, _originalName, StringComparison.Ordinal))
        {
            Name = _originalName;
        }

        if (!string.Equals(VariablesJson, _originalVariablesJson, StringComparison.Ordinal))
        {
            VariablesJson = _originalVariablesJson;
        }
    }
    
    private bool CanRevert() => IsDirty;

    /// <summary>
    /// Formats the JSON variables for the environment item.
    /// </summary>
    [RelayCommand]
    private void FormatJson()
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(VariablesJson);
            VariablesJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            // Ignore formatting errors
        }
    }

    /// <summary>
    /// Marks the current environment as active by setting it in the parent
    /// view model, unless it is a global environment.
    /// </summary>
    [RelayCommand]
    private async Task SetActive()
    {
        if (IsGlobal)
        {
            return; // Globals can't be active
        }

        await _parent.SetActiveEnvironmentAsync(this);
    }

    /// <summary>
    /// Invokes the parent view model's method to select this environment item as the active environment.
    /// </summary>
    [RelayCommand]
    private void Select()
    {
        _parent.SelectEnvironment(this);
    }

    /// <summary>
    /// Deletes the current environment item from the parent view model.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        await _parent.DeleteEnvironmentAsync(this);
    }
    
    private bool CanDelete() => !IsGlobal;

    /// <summary>
    /// Duplicates the current environment item by invoking the parent view model's method.'
    /// </summary>
    [RelayCommand]
    private async Task Duplicate()
    {
        await _parent.DuplicateEnvironmentAsync(this);
    }

    /// <summary>
    /// Attempts to parse a JSON string and checks if it is valid.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <returns>True if the JSON string is valid; otherwise, false.</returns>
    private static bool TryParseJson(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Invoked when the name property changes. This method verifies
    /// if the name modification affects the current state and updates
    /// the dirty check accordingly.
    /// </summary>
    /// <param name="value">The new value of the name property.</param>
    partial void OnNameChanged(string value) => CheckDirtyFast();

    /// <summary>
    /// Handles changes to the JSON representation of environment variables.
    /// </summary>
    /// <param name="value">The updated JSON string for environment variables.</param>
    partial void OnVariablesJsonChanged(string value) => CheckDirtyFast();

    /// <summary>
    /// Performs a quick validation to determine if the current state of the view model
    /// differs from its original snapshot. This method checks for changes in relevant
    /// properties, such as name and JSON variables, and updates the dirty flag accordingly.
    /// </summary>
    private void CheckDirtyFast()
    {
        bool nameChanged = !string.Equals(Name, _originalName, StringComparison.Ordinal);
        bool jsonChanged = !string.Equals(VariablesJson, _originalVariablesJson, StringComparison.Ordinal);
        
        IsDirty = nameChanged || jsonChanged;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}
