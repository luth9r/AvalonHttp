using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class FilePickerService : IFilePickerService
{
    private IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.StorageProvider;
        }

        return null;
    }

    public async Task<IStorageFile?> OpenFileAsync(string title, IReadOnlyList<FilePickerFileType>? filters = null)
    {
        var provider = GetStorageProvider();
        if (provider == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        };

        var files = await provider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0] : null;
    }

    public async Task<IStorageFile?> SaveFileAsync(string title, string defaultExtension, string suggestedFileName, IReadOnlyList<FilePickerFileType>? filters = null)
    {
        var provider = GetStorageProvider();
        if (provider == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = filters
        };

        return await provider.SaveFilePickerAsync(options);
    }
}
