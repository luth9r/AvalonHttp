using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.ViewModels.EnvironmentAggregate;

public partial class EnvironmentItemViewModel : ViewModelBase
{
    private readonly EnvironmentsViewModel _parent;
    private readonly Environment _environment;
    
    // ========================================
    // Original values for cancel operations
    // ========================================
    
    private string _originalName = string.Empty;
    private string _originalJson = string.Empty;

    // ========================================
    // Properties (single source of truth)
    // ========================================
    
    public Environment Environment => _environment;
    public Guid Id => _environment.Id;
    public bool IsGlobal => _environment.IsGlobal;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJsonValid))]
    [NotifyPropertyChangedFor(nameof(JsonErrorMessage))]
    [NotifyPropertyChangedFor(nameof(VariablesCount))]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _variablesJson;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isSelected;

    // Computed properties
    public bool IsJsonValid => TryParseJson(VariablesJson);
    
    public string JsonErrorMessage => IsJsonValid 
        ? string.Empty 
        : "Invalid JSON format";
    
    public int VariablesCount
    {
        get
        {
            try
            {
                if (!IsJsonValid) return 0;
                
                using var doc = JsonDocument.Parse(VariablesJson);
                return doc.RootElement.EnumerateObject().Count();
            }
            catch
            {
                return 0;
            }
        }
    }

    public bool IsDirty
    {
        get
        {
            if (_environment.Name != Name) return true;
            if (_environment.VariablesJson != VariablesJson) return true;
            return false;
        }
    }

    // ========================================
    // Constructor
    // ========================================
    
    public EnvironmentItemViewModel(Environment environment, EnvironmentsViewModel parent)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));

        // Initialize from model (single direction)
        LoadFromModel();
    }

    // ========================================
    // Load/Save Model
    // ========================================
    
    private void LoadFromModel()
    {
        _name = _environment.Name;
        _variablesJson = _environment.VariablesJson;
        _originalName = _name;
        _originalJson = _variablesJson;
    }

    public void ApplyToModel()
    {
        _environment.Name = Name;
        _environment.VariablesJson = VariablesJson;
        
        // Reset dirty state
        _originalName = Name;
        _originalJson = VariablesJson;
        OnPropertyChanged(nameof(IsDirty));
    }

    public void RevertChanges()
    {
        Name = _originalName;
        VariablesJson = _originalJson;
        OnPropertyChanged(nameof(IsDirty));
    }

    // ========================================
    // Commands - Name Editing
    // ========================================
    
    [RelayCommand]
    private void StartRename()
    {
        if (IsGlobal) return; // Globals can't be renamed
        
        _originalName = Name;
        IsEditing = true;
    }

    [RelayCommand(CanExecute = nameof(CanFinishRename))]
    private async Task FinishRename()
    {
        if (!CanFinishRename())
        {
            CancelRename();
            return;
        }

        IsEditing = false;
        await SaveAsync();
    }

    private bool CanFinishRename()
    {
        if (string.IsNullOrWhiteSpace(Name)) return false;
        if (Name == _originalName) return true; // No changes
        
        // Check if name is unique (optional)
        return true;
    }

    [RelayCommand]
    private void CancelRename()
    {
        Name = _originalName;
        IsEditing = false;
    }

    // ========================================
    // Commands - JSON Editing
    // ========================================
    
    [RelayCommand(CanExecute = nameof(CanSaveJson))]
    private async Task SaveJson()
    {
        await SaveAsync();
    }

    private bool CanSaveJson() => IsJsonValid;

    [RelayCommand]
    private void RevertJson()
    {
        VariablesJson = _originalJson;
    }

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

    // ========================================
    // Commands - Environment Actions
    // ========================================
    
    [RelayCommand]
    private async Task SetActive()
    {
        if (IsGlobal) return; // Globals can't be active
        
        await _parent.SetActiveEnvironmentAsync(this);
    }

    [RelayCommand]
    private void Select()
    {
        _parent.SelectEnvironment(this);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        await _parent.DeleteEnvironmentAsync(this);
    }
    
    private bool CanDelete() => !IsGlobal;

    [RelayCommand]
    private async Task Duplicate()
    {
        await _parent.DuplicateEnvironmentAsync(this);
    }

    // ========================================
    // Save Logic
    // ========================================
    
    private async Task SaveAsync()
    {
        if (!IsJsonValid)
        {
            // Show error to user via parent
            return;
        }

        // Apply changes to model
        ApplyToModel();
        
        // Delegate save to parent
        await _parent.SaveEnvironmentAsync(this);
    }

    // ========================================
    // Helper Methods
    // ========================================
    
    private static bool TryParseJson(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return true;
            
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }
}
