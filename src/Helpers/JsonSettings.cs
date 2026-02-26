using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvalonHttp.Helpers;

public static class JsonSettings
{
    public static readonly JsonSerializerOptions Default = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions Indented = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions IndentedUnsafe = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly JsonSerializerOptions IgnoreCycles = new JsonSerializerOptions
    {
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
}
