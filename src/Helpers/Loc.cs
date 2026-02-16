using Avalonia;
using Avalonia.Controls;

namespace AvalonHttp.Helpers;

public static class Loc
{
    public static string Tr(string key, params object[] args)
    {
        if (Application.Current == null) return $"#{key}#_NO_APP";

        if (Application.Current.TryFindResource(key, out var res) && res is string s)
        {
            if (args.Length > 0)
            {
                try
                {
                    return string.Format(s, args);
                }
                catch
                {
                    return s; // Fallback if format fails
                }
            }
            return s;
        }

        return $"#{key}#";
    }
}
