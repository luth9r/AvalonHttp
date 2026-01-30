using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace AvalonHttp.Controls;

public partial class JsonEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<JsonEditor, string>(
            nameof(Text), 
            string.Empty,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    
    public static readonly StyledProperty<bool> HasContentProperty =
        AvaloniaProperty.Register<JsonEditor, bool>(
            nameof(HasContent));
    
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public bool HasContent
    {
        get => GetValue(HasContentProperty);
        private set => SetValue(HasContentProperty, value);
    }
    
    private TextEditor? _editor;
    private TextMate.Installation? _textMateInstallation;
    private bool _isUpdating;

    public JsonEditor()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        _editor = this.FindControl<TextEditor>("Editor");
        
        if (_editor != null)
        {
            _editor.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            
            _editor.Document = new TextDocument();
            
            _editor.Options = new TextEditorOptions
            {
                ShowSpaces = false,
                ShowTabs = false,
                ShowEndOfLine = false,
                HighlightCurrentLine = false,
                IndentationSize = 2,
                ConvertTabsToSpaces = true,
                EnableHyperlinks = false,
                EnableEmailHyperlinks = false,
                AllowScrollBelowDocument = false
            };
            
            SetupTextMate();
            
            _editor.TextChanged += (_, _) =>
            {
                if (!_isUpdating && _editor.Document != null)
                {
                    _isUpdating = true;
                    SetValue(TextProperty, _editor.Document.Text);
                    _isUpdating = false;
                }
            };
            
            _editor.Loaded += OnEditorLoaded;
        }
    }
    
    private void OnEditorLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_editor?.TextArea == null) return;

        foreach (var margin in _editor.TextArea.LeftMargins)
        {
            if (margin is LineNumberMargin lineNumbers)
            {
                lineNumbers.SetValue(TextBlock.FontFamilyProperty, _editor.FontFamily);
                lineNumbers.SetValue(TextBlock.FontSizeProperty, _editor.FontSize);
                lineNumbers.Margin = new Thickness(0, 1, 8, 0); 
            }
        }

        _editor.TextArea.TextView.Redraw();
    }
    
    private void SetupTextMate()
    {
        if (_editor == null) return;

        try
        {
            var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            
            _textMateInstallation = _editor.InstallTextMate(registryOptions);
            
            _textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(
                registryOptions.GetLanguageByExtension(".json").Id));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TextMate error: {ex.Message}");
        }
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == TextProperty && !_isUpdating && _editor?.Document != null)
        {
            var newText = change.GetNewValue<string>();
            
            HasContent = !string.IsNullOrWhiteSpace(newText);
            
            if (_editor.Document.Text == newText)
                return;
            
            _isUpdating = true;
            _editor.Document.Text = newText;
            
            Dispatcher.UIThread.Post(() =>
            {
                _editor?.TextArea?.TextView?.Redraw();
            }, DispatcherPriority.Background);
            
            _isUpdating = false;
        }
    }
}
