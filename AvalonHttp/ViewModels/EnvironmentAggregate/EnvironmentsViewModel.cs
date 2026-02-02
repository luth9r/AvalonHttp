using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.ViewModels.EnvironmentAggregate;

public partial class EnvironmentsViewModel : ViewModelBase
{
    private readonly IEnvironmentRepository _environmentRepository;

    // ========================================
    // Observable Collections
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnvironments))]
    private ObservableCollection<EnvironmentItemViewModel> _environments = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveEnvironment))]
    private EnvironmentItemViewModel? _activeEnvironment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEnvironment))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveEnvironmentCommand))]
    private EnvironmentItemViewModel? _selectedEnvironment;

    // ========================================
    // Computed Properties
    // ========================================
    
    public bool HasEnvironments => Environments.Count > 0;
    public bool HasActiveEnvironment => ActiveEnvironment != null;
    public bool HasSelectedEnvironment => SelectedEnvironment != null;

    public EnvironmentItemViewModel? GlobalEnvironment =>
        Environments.FirstOrDefault(e => e.IsGlobal);

    // Use IsDirty from ItemViewModel
    public bool HasUnsavedChanges => SelectedEnvironment?.IsDirty ?? false;

    // ========================================
    // Events
    // ========================================
    
    public event EventHandler<Environment?>? ActiveEnvironmentChanged;

    // ========================================
    // Constructor
    // ========================================
    
    public EnvironmentsViewModel(IEnvironmentRepository environmentRepository)
    {
        _environmentRepository =
            environmentRepository ?? throw new ArgumentNullException(nameof(environmentRepository));
    }

    // ========================================
    // Initialization
    // ========================================
    
    public async Task InitializeAsync()
    {
        await LoadEnvironmentsAsync();

        // Select active or first environment
        if (SelectedEnvironment == null)
        {
            SelectEnvironment(ActiveEnvironment ?? Environments.FirstOrDefault());
        }
    }

    private async Task LoadEnvironmentsAsync()
    {
        try
        {
            var environments = await _environmentRepository.EnsureDefaultEnvironmentsAsync();

            Environments.Clear();
            foreach (var env in environments)
            {
                var itemVm = new EnvironmentItemViewModel(env, this);
                
                // Subscribe to property changes to refresh HasUnsavedChanges
                itemVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(EnvironmentItemViewModel.IsDirty))
                    {
                        OnPropertyChanged(nameof(HasUnsavedChanges));
                    }
                };
                
                Environments.Add(itemVm);
            }

            // Load and set active environment
            var activeEnv = await _environmentRepository.GetActiveEnvironmentAsync();
            if (activeEnv != null)
            {
                var activeVm = Environments.FirstOrDefault(e => e.Id == activeEnv.Id);
                if (activeVm != null)
                {
                    await SetActiveEnvironmentAsync(activeVm);
                }
            }
            else if (Environments.Any(e => !e.IsGlobal))
            {
                // Set first non-global as active
                await SetActiveEnvironmentAsync(Environments.First(e => !e.IsGlobal));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load environments: {ex.Message}");
        }
    }

    // ========================================
    // Selection Logic
    // ========================================
    
    [RelayCommand]
    public void Select(EnvironmentItemViewModel? environment)
    {
        SelectEnvironment(environment);
    }

    public void SelectEnvironment(EnvironmentItemViewModel? environment)
    {
        if (SelectedEnvironment == environment) return;

        if (SelectedEnvironment != null)
        {
            SelectedEnvironment.IsSelected = false;
        }

        SelectedEnvironment = environment;

        if (SelectedEnvironment != null)
        {
            SelectedEnvironment.IsSelected = true;
        }
    }

    // ========================================
    // Save Logic
    // ========================================
    
    [RelayCommand(CanExecute = nameof(CanSaveEnvironment))]
    private async Task SaveEnvironment()
    {
        if (SelectedEnvironment == null) return;

        await SaveEnvironmentAsync(SelectedEnvironment);
    }

    private bool CanSaveEnvironment() => SelectedEnvironment?.IsDirty ?? false;

    public async Task SaveEnvironmentAsync(EnvironmentItemViewModel environment)
    {
        try
        {
            if (!environment.IsJsonValid)
            {
                // TODO: Show error to user
                System.Diagnostics.Debug.WriteLine($"Cannot save '{environment.Name}': Invalid JSON");
                return;
            }

            // Apply ViewModel changes to Model
            environment.ApplyToModel();

            // Save to disk
            await _environmentRepository.SaveAsync(environment.Environment);

            System.Diagnostics.Debug.WriteLine($"💾 Saved '{environment.Name}' to disk");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save environment '{environment.Name}': {ex.Message}");
        }
    }

    // ========================================
    // Create/Delete/Duplicate
    // ========================================
    
    [RelayCommand]
    private async Task CreateEnvironment()
    {
        try
        {
            var environment = new Environment
            {
                Name = GenerateUniqueName("New Environment"),
                VariablesJson = "{\n  \n}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _environmentRepository.SaveAsync(environment);

            var viewModel = new EnvironmentItemViewModel(environment, this);
            Environments.Add(viewModel);

            // Select and start rename
            SelectEnvironment(viewModel);
            viewModel.StartRenameCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create environment: {ex.Message}");
        }
    }

    public async Task DeleteEnvironmentAsync(EnvironmentItemViewModel environment)
    {
        try
        {
            await _environmentRepository.DeleteAsync(environment.Id);
            Environments.Remove(environment);

            // Update active environment if deleted
            if (ActiveEnvironment == environment)
            {
                var newActive = Environments.FirstOrDefault(e => !e.IsGlobal);
                await SetActiveEnvironmentAsync(newActive);
            }

            // Update selection if deleted
            if (SelectedEnvironment == environment)
            {
                SelectEnvironment(Environments.FirstOrDefault());
            }

            System.Diagnostics.Debug.WriteLine($"🗑️ Deleted environment '{environment.Name}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete environment: {ex.Message}");
        }
    }

    public async Task DuplicateEnvironmentAsync(EnvironmentItemViewModel environment)
    {
        try
        {
            var newEnv = new Environment
            {
                Name = GenerateUniqueName($"{environment.Name} (Copy)"),
                VariablesJson = environment.VariablesJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _environmentRepository.SaveAsync(newEnv);

            var viewModel = new EnvironmentItemViewModel(newEnv, this);
            Environments.Add(viewModel);

            // Select duplicated environment
            SelectEnvironment(viewModel);

            System.Diagnostics.Debug.WriteLine($"Duplicated environment '{environment.Name}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to duplicate environment: {ex.Message}");
        }
    }

    // ========================================
    // Active Environment
    // ========================================
    
    public async Task SetActiveEnvironmentAsync(EnvironmentItemViewModel? environment)
    {
        if (ActiveEnvironment == environment) return;

        if (ActiveEnvironment != null)
        {
            ActiveEnvironment.IsActive = false;
        }

        ActiveEnvironment = environment;

        if (environment != null)
        {
            environment.IsActive = true;
            await _environmentRepository.SetActiveEnvironmentAsync(environment.Id);
        }
        else
        {
            await _environmentRepository.SetActiveEnvironmentAsync(null);
        }

        ActiveEnvironmentChanged?.Invoke(this, environment?.Environment);
        
        System.Diagnostics.Debug.WriteLine($"✅ Active environment: {environment?.Name ?? "None"}");
    }

    // ========================================
    // Helper Methods
    // ========================================
    
    private string GenerateUniqueName(string baseName)
    {
        var name = baseName;
        var counter = 1;
        
        while (Environments.Any(e => e.Name == name))
        {
            name = $"{baseName} {counter++}";
        }

        return name;
    }

    // ========================================
    // Variable Resolution Logic
    // ========================================
    
    public string ResolveVariables(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Get variables from active and global environments
        var activeVars = ActiveEnvironment?.Environment.GetVariables() 
            ?? new Dictionary<string, string>();
        var globalVars = GlobalEnvironment?.Environment.GetVariables() 
            ?? new Dictionary<string, string>();

        // Merge dictionaries (active overrides global)
        var mergedVars = new Dictionary<string, string>(globalVars);
        foreach (var kvp in activeVars)
        {
            mergedVars[kvp.Key] = kvp.Value;
        }

        var result = text;

        // 1. System variables ($guid, $timestamp, etc.)
        result = ResolveSystemVariables(result);

        // 2. URL-encoded variables (%7B%7BVar%7D%7D) - important for query params
        var regexEncoded = new Regex(@"%7B%7B(.*?)%7D%7D", RegexOptions.IgnoreCase);
        result = ReplaceVariables(result, regexEncoded, mergedVars);

        // 3. Standard variables ({{Var}})
        var regexStandard = new Regex(@"\{\{([^}]+)\}\}");
        result = ReplaceVariables(result, regexStandard, mergedVars);

        return result;
    }

    private string ReplaceVariables(string text, Regex regex, Dictionary<string, string> variables)
    {
        var result = text;
        var maxIterations = 10; // Prevent infinite loops
        var iteration = 0;

        while (iteration < maxIterations)
        {
            var hasReplacement = false;
            
            result = regex.Replace(result, match =>
            {
                var variableName = match.Groups[1].Value.Trim();

                // Skip system variables
                if (variableName.StartsWith("$")) return match.Value;

                if (variables.TryGetValue(variableName, out var value))
                {
                    hasReplacement = true;
                    return value;
                }
                
                return match.Value; // Leave unresolved
            });

            if (!hasReplacement) break;
            iteration++;
        }
        
        return result;
    }

    private string ResolveSystemVariables(string text)
    {
        var regex = new Regex(@"\{\{\$([^}]+)\}\}");

        return regex.Replace(text, match =>
        {
            var variableName = match.Groups[1].Value.Trim().ToLower();

            return variableName switch
            {
                "guid" => Guid.NewGuid().ToString(),
                "timestamp" => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                "isotimestamp" => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                "randomint" => Random.Shared.Next(0, 1000).ToString(),
                "date" => DateTime.Now.ToString("yyyy-MM-dd"),
                "time" => DateTime.Now.ToString("HH:mm:ss"),
                "datetime" => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                _ => match.Value
            };
        });
    }
}
