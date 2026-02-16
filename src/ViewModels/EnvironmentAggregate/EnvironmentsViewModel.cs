using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvalonHttp.Common.Constants;
using AvalonHttp.Helpers;
using AvalonHttp.Messages;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.ViewModels.EnvironmentAggregate;

/// <summary>
/// Represents the view model for managing environments in the application.
/// </summary>
/// <remarks>
/// This class provides functionality to handle operations such as
/// loading, selecting, saving, deleting, duplicating, and activating environments.
/// It also provides utilities for resolving variables and tracks states like
/// whether there are unsaved changes or selected/active environments.
/// </remarks>
public partial class EnvironmentsViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Reference to environment repository
    /// </summary>
    private readonly IEnvironmentRepository _environmentRepository;

    /// <summary>
    /// Collection of environment items managed within the view model.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEnvironments))]
    private ObservableCollection<EnvironmentItemViewModel> _environments = new();

    /// <summary>
    /// Indicates whether an environment is currently active.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveEnvironment))]
    private EnvironmentItemViewModel? _activeEnvironment;

    /// <summary>
    /// Indicates whether an environment is currently selected.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEnvironment))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveEnvironmentCommand))]
    private EnvironmentItemViewModel? _selectedEnvironment;
    
    public bool HasEnvironments => Environments.Count > 0;
    public bool HasActiveEnvironment => ActiveEnvironment != null;
    public bool HasSelectedEnvironment => SelectedEnvironment != null;

    /// <summary>
    /// Represents the global environment within the view model.
    /// </summary>
    [ObservableProperty]
    private EnvironmentItemViewModel? _globalEnvironment;

    /// <summary>
    /// Indicates whether there are unsaved changes in any of the environments.
    /// Returns true if at least one environment is marked as dirty; otherwise, false.
    /// </summary>
    public bool HasUnsavedChanges => Environments.Any(e => e.IsDirty);
    
    public EnvironmentsViewModel(IEnvironmentRepository environmentRepository)
    {
        _environmentRepository =
            environmentRepository ?? throw new ArgumentNullException(nameof(environmentRepository));
    }

    /// <summary>
    /// Initializes the environments view model by loading environments and selecting the active or first environment.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadEnvironmentsAsync();

        // Select active or first environment
        if (SelectedEnvironment == null)
        {
            SelectEnvironment(ActiveEnvironment ?? Environments.FirstOrDefault());
        }
    }

    /// <summary>
    /// Asynchronously loads all available environments, resets the current environment view models,
    /// and sets the active or first non-global environment.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task LoadEnvironmentsAsync()
    {
        try
        {
            // Load environments from disk
            var environments = await _environmentRepository.EnsureDefaultEnvironmentsAsync();

            // Reset view models
            foreach (var oldVm in Environments)
            {
                oldVm.PropertyChanged -= OnEnvironmentItemPropertyChanged;
                oldVm.Dispose();
            }

            Environments.Clear();
            
            GlobalEnvironment = null;
            
            // Create view models from environments
            foreach (var env in environments)
            {
                var itemVm = CreateEnvironmentViewModel(env); 
                Environments.Add(itemVm);
                
                if (itemVm.IsGlobal)
                {
                    GlobalEnvironment = itemVm;
                }
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
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                Loc.Tr("MsgFailedToLoadEnvironments"),
                Loc.Tr("MsgFailedToLoadEnvironmentsDetail", ex.Message)
            ));
            System.Diagnostics.Debug.WriteLine($"Failed to load environments: {ex.Message}");
        }
    }

    /// <summary>
    /// Selects the specified environment, updating the application state to reflect the new selection.
    /// </summary>
    /// <param name="environment">The environment to be selected. Pass null to clear the current selection.</param>
    [RelayCommand]
    public void Select(EnvironmentItemViewModel? environment)
    {
        SelectEnvironment(environment);
    }

    /// <summary>
    /// Selects the specified environment, updating the application state to reflect the new selection.
    /// </summary>
    /// <param name="environment">The environment to be selected.</param>
    public void SelectEnvironment(EnvironmentItemViewModel? environment)
    {
        if (SelectedEnvironment == environment)
        {
            return;
        }

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

    /// <summary>
    /// Saves the currently selected environment if it has unsaved changes.
    /// </summary>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <remarks>This method is bound to the Save Environment command and being used only bu UI.</remarks>
    [RelayCommand(CanExecute = nameof(CanSaveEnvironment))]
    private async Task SaveEnvironment()
    {
        if (SelectedEnvironment == null)
        {
            return;
        }

        await SaveEnvironmentAsync(SelectedEnvironment);
    }

    private bool CanSaveEnvironment() => SelectedEnvironment?.IsDirty ?? false;

    /// <summary>
    /// Saves the specified environment to persistent storage after validating its JSON structure and applying all changes made in the ViewModel.
    /// </summary>
    /// <param name="environment">The environment to be saved, represented as an instance of <c>EnvironmentItemViewModel</c>.</param>
    /// <returns>A <c>Task</c> representing the asynchronous save operation.</returns>
    /// <remarks>This method could be used by other parts of the application to save environments programmatically.</remarks>
    public async Task SaveEnvironmentAsync(EnvironmentItemViewModel environment)
    {
        try
        {
            if (!environment.IsJsonValid)
            {
                WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                    Loc.Tr("DialogTitleInvalidConfig"),
                    Loc.Tr("MsgInvalidJson")
                ));
                System.Diagnostics.Debug.WriteLine($"Cannot save '{environment.Name}': Invalid JSON");
                return;
            }

            // Apply ViewModel changes to Model
            environment.ApplyToModel();

            // Save to disk
            await _environmentRepository.SaveAsync(environment.Environment);
            
            environment.UpdateSnapshot();

            System.Diagnostics.Debug.WriteLine($"💾 Saved '{environment.Name}' to disk");
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                Loc.Tr("DialogTitleSaveFailure"),
                Loc.Tr("MsgSaveEnvironmentError", ex.Message)
            ));
            System.Diagnostics.Debug.WriteLine($"Cannot save '{environment.Name}': Invalid JSON");
        }
    }

    /// <summary>
    /// Creates a new environment and adds it to the collection of environments.
    /// </summary>
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

            var viewModel = CreateEnvironmentViewModel(environment);
            Environments.Add(viewModel);

            // Select and start rename
            SelectEnvironment(viewModel);
            viewModel.StartRenameCommand.Execute(null);
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                Loc.Tr("DialogTitleCreateFailure"),
                Loc.Tr("MsgCreateEnvironmentError", ex.Message)
            ));
            System.Diagnostics.Debug.WriteLine($"Cannot create new environment: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Deletes the specified environment from the collection of environments.
    /// </summary>
    /// <param name="environment">The environment to be deleted.</param>
    public async Task DeleteEnvironmentAsync(EnvironmentItemViewModel environment)
    {
        if (environment == null)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(DialogMessage.Destructive(
            Loc.Tr("DialogTitleDeleteEnvironment"),
            Loc.Tr("MsgDeleteEnvironmentConfirm", environment.Name),
            confirmText: Loc.Tr("BtnDelete"),
            onConfirm: async () => 
            {
                try
                {
                    // Delete environment from disk
                    await _environmentRepository.DeleteAsync(environment.Id);
                
                    // Remove from view model collection
                    environment.PropertyChanged -= OnEnvironmentItemPropertyChanged;
                    environment.Dispose();
                
                    Environments.Remove(environment);
                
                    // Update active environment if necessary
                    await UpdateActiveEnvironmentAfterDeletionAsync(environment);
                    UpdateSelectionAfterDeletion(environment);
                
                    System.Diagnostics.Debug.WriteLine($"✅ Environment '{environment.Name}' deleted");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete environment: {ex.Message}");
                
                    WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                        Loc.Tr("DialogTitleDeleteFailure"),
                        Loc.Tr("MsgDeleteEnvironmentError", ex.Message)
                    ));
                }
            }
        ));
    }

    /// <summary>
    /// Updates the active environment after the specified environment has been deleted.
    /// </summary>
    /// <param name="deletedEnvironment">The environment that was deleted.</param>
    private async Task UpdateActiveEnvironmentAfterDeletionAsync(EnvironmentItemViewModel deletedEnvironment)
    {
        if (ActiveEnvironment == deletedEnvironment)
        {
            var newActive = Environments.FirstOrDefault(e => !e.IsGlobal);
            await SetActiveEnvironmentAsync(newActive);
        }
    }

    /// <summary>
    /// Updates the selected environment after the specified environment has been deleted.
    /// </summary>
    /// <param name="deletedEnvironment">The environment that was deleted.</param>
    private void UpdateSelectionAfterDeletion(EnvironmentItemViewModel deletedEnvironment)
    {
        if (SelectedEnvironment == deletedEnvironment)
        {
            SelectEnvironment(Environments.FirstOrDefault());
        }
    }

    /// <summary>
    /// Duplicates the specified environment and adds it to the collection of environments.
    /// </summary>
    /// <param name="environment">The environment to be duplicated.</param>
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

            var viewModel = CreateEnvironmentViewModel(newEnv);
            Environments.Add(viewModel);

            // Select duplicated environment
            SelectEnvironment(viewModel);

            System.Diagnostics.Debug.WriteLine($"Duplicated environment '{environment.Name}'");
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                Loc.Tr("DialogTitleDuplicateFailure"),
                Loc.Tr("MsgDuplicateEnvironmentError", ex.Message)
            ));
            System.Diagnostics.Debug.WriteLine($"Cannot duplicate '{environment.Name}': {ex.Message}");
        }
    }
    
    /// <summary>
    /// Sets the specified environment as the active environment.
    /// </summary>
    /// <param name="environment">The environment to be set as active.</param>
    public async Task SetActiveEnvironmentAsync(EnvironmentItemViewModel? environment)
    {
        if (ActiveEnvironment == environment)
        {
            return;
        }

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
        
        System.Diagnostics.Debug.WriteLine($"✅ Active environment: {environment?.Name ?? "None"}");
    }
    
    /// <summary>
    /// Generates a unique name based on the specified base name by appending a number to the end of the base name if it already exists in the collection of environments.
    /// </summary>
    /// <param name="baseName">The base name to use as a starting point for generating a unique name.</param>
    /// <returns>A unique name that is not already in use by any environment in the collection.</returns>
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

    /// <summary>
    /// Exits edit mode for all environments in the collection by executing the command
    /// to finalize any ongoing renaming process for each environment.
    /// </summary>
    /// <remarks>This method <c>Cancels</c> all changes.</remarks>
    [RelayCommand]
    private void CancelAllRenamesEditModes()
    {
        foreach (var environment in Environments)
        {
            if (environment.IsEditing)
            {
                environment.CancelRenameCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Resolves variables within a given text by replacing placeholders with corresponding values from active
    /// and global environments, as well as system-defined variables.
    /// </summary>
    /// <param name="text">The input text containing variable placeholders.</param>
    /// <returns>The resolved text with variable placeholders replaced by their corresponding values.</returns>
    public string ResolveVariables(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Get variables from active and global environments
        var activeVars = ActiveEnvironment?.Environment.GetVariables() 
            ?? new Dictionary<string, string>();
        var globalVars = GlobalEnvironment?.Environment.GetVariables() 
            ?? new Dictionary<string, string>();

        // Merge dictionaries (active overrides global)
        var mergedVars = new Dictionary<string, string>(globalVars, StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Replaces variables in the given text using the specified regular expression and a dictionary of variables.
    /// </summary>
    /// <param name="text">The input text in which placeholders will be replaced with variable values.</param>
    /// <param name="regex">The regular expression used to identify placeholders within the text.</param>
    /// <param name="variables">A dictionary containing variable names and their corresponding values. Placeholders matching keys in the dictionary will be replaced with associated values.</param>
    /// <returns>The processed text with variables replaced, or the original text if no replacements are made.</returns>
    private static string ReplaceVariables(string text, Regex regex, Dictionary<string, string> variables)
    {
        if (variables.Count == 0)
        {
            return text;
        }

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
                if (variableName.StartsWith("$"))
                {
                    return match.Value;
                }

                if (variables.TryGetValue(variableName, out var value))
                {
                    hasReplacement = true;
                    return value;
                }
                
                return match.Value; // Leave unresolved
            });

            if (!hasReplacement)
            {
                break;
            }

            iteration++;
        }
        
        return result;
    }

    /// <summary>
    /// Resolves system-defined variables by replacing placeholders with their corresponding values.
    /// </summary>
    /// <param name="text">The input text containing system variable placeholders.</param>
    /// <returns>The processed text with system variables replaced, or the original text if no replacements are made.</returns>
    private static string ResolveSystemVariables(string text)
    {
        var regex = new Regex(@"\{\{\$([^}]+)\}\}");

        return regex.Replace(text, match =>
        {
            var variableName = match.Groups[1].Value.Trim().ToLower();

            return variableName switch
            {
                SystemVariables.Guid => Guid.NewGuid().ToString(),
                SystemVariables.Timestamp => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                SystemVariables.IsoTimestamp => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                SystemVariables.RandomInt => Random.Shared.Next(0, 1000).ToString(),
                SystemVariables.Date => DateTime.Now.ToString("yyyy-MM-dd"),
                SystemVariables.Time => DateTime.Now.ToString("HH:mm:ss"),
                SystemVariables.DateTime => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                _ => match.Value
            };
        });
    }

    /// <summary>
    /// Handles the event when the selected environment changes, updating related commands or properties accordingly.
    /// </summary>
    /// <param name="value">The newly selected environment, or null if no environment is selected.</param>
    partial void OnSelectedEnvironmentChanged(EnvironmentItemViewModel? value)
    {
        SaveEnvironmentCommand.NotifyCanExecuteChanged();
    }
    
    /// <summary>
    /// Handles the event when any environment property changes, updating related commands or properties accordingly.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnEnvironmentItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnvironmentItemViewModel.IsDirty))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (sender == SelectedEnvironment)
            {
                SaveEnvironmentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Creates a new instance of <see cref="EnvironmentItemViewModel"/> for a given <see cref="Environment"/> and links it to the current environment view model.
    /// </summary>
    /// <param name="env">The environment data used to initialize the view model.</param>
    /// <returns>A view model representing the given environment.</returns>
    private EnvironmentItemViewModel CreateEnvironmentViewModel(Environment env)
    {
        var vm = new EnvironmentItemViewModel(env, this);
        vm.PropertyChanged += OnEnvironmentItemPropertyChanged; 
        return vm;
    }

    /// <summary>
    /// Releases all resources used by the EnvironmentsViewModel instance.
    /// This includes unsubscribing from property changed events and disposing
    /// of all associated EnvironmentItemViewModel objects in the Environments collection.
    /// </summary>
    public void Dispose()
    {
        foreach (var env in Environments)
        {
            env.PropertyChanged -= OnEnvironmentItemPropertyChanged;
            env.Dispose();
        }
    
        Environments.Clear();
    }
}

