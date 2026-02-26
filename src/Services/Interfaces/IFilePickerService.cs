using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace AvalonHttp.Services.Interfaces;

public interface IFilePickerService
{
    Task<IStorageFile?> OpenFileAsync(string title, IReadOnlyList<FilePickerFileType>? filters = null);
    
    Task<IStorageFile?> SaveFileAsync(string title, string defaultExtension, string suggestedFileName, IReadOnlyList<FilePickerFileType>? filters = null);
}
