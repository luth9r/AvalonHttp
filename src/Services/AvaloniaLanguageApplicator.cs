using System;
using System.Linq;
using AvalonHttp.Services.Interfaces;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;

namespace AvalonHttp.Services;

public class AvaloniaLanguageApplicator : ILanguageApplicator
{
    public void ApplyLanguage(string cultureCode)
    {
        Action switchAction = () =>
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var newUri = new Uri($"avares://AvalonHttp/Assets/Lang/{cultureCode}.axaml");
                var dictionaries = app.Resources.MergedDictionaries;

                var existingDictionary = dictionaries.OfType<ResourceInclude>()
                    .FirstOrDefault(d => d.Source?.AbsolutePath?.Contains("/Assets/Lang/") == true);

                if (existingDictionary?.Source?.ToString() == newUri.ToString())
                {
                    return;
                }
                
                var newResourceInclude = new ResourceInclude(newUri) { Source = newUri };
                
                dictionaries.Insert(0, newResourceInclude);
                
                if (existingDictionary != null)
                {
                    dictionaries.Remove(existingDictionary);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying language dictionary: {ex.Message}");
            }
        };
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            switchAction();
        }
        else
        {
            Dispatcher.UIThread.Invoke(switchAction);
        }
    }
}