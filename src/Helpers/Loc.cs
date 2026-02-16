using Avalonia;
using Avalonia.Controls;

namespace AvalonHttp.Helpers;

public static class Loc
{
    public static string Tr(string key)
    {
        if (Application.Current == null) return $"#{key}#_NO_APP";

        if (Application.Current.TryFindResource(key, out var res) && res is string s)
        {
            return s;
        }

        return $"#{key}#";
    }
}
