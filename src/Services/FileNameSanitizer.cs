using System.IO;
using System.Linq;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class FileNameSanitizer : IFileNameSanitizer
{
    private const int MaxLength = 50;
    
    public string Sanitize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Unnamed";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        
        return sanitized.Length > MaxLength 
            ? sanitized[..MaxLength] 
            : sanitized;
    }
}