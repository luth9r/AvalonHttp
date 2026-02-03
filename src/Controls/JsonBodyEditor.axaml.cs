using System;
using System.Linq;
using System.Net.Mime;
using AvalonHttp.Common.FoldingStrategies;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace AvalonHttp.Controls;

public partial class JsonBodyEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<JsonBodyEditor, string>(
            nameof(Text), 
            string.Empty,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    
    public static readonly StyledProperty<string> ContentTypeProperty =
        AvaloniaProperty.Register<JsonBodyEditor, string>(
            nameof(ContentType), 
            "json");
    
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<JsonBodyEditor, bool>(
            nameof(IsReadOnly), 
            false);
    
    public static readonly StyledProperty<Thickness> EditorPaddingProperty =
        AvaloniaProperty.Register<JsonBodyEditor, Thickness>(
            nameof(EditorPadding));
    
    public static readonly StyledProperty<Thickness> EditorMarginProperty =
        AvaloniaProperty.Register<JsonBodyEditor, Thickness>(
            nameof(EditorMargin));
    
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public string ContentType
    {
        get => GetValue(ContentTypeProperty);
        set => SetValue(ContentTypeProperty, value);
    }
    
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }
    
    public Thickness EditorPadding
    {
        get => GetValue(EditorPaddingProperty);
        set => SetValue(EditorPaddingProperty, value);
    }
    
    public Thickness EditorMargin
    {
        get => GetValue(EditorMarginProperty);
        set => SetValue(EditorMarginProperty, value);
    }
    
    private TextEditor? _editor;
    private TextMate.Installation? _textMateInstallation;
    private FoldingManager? _foldingManager;
    private object? _currentFoldingStrategy;
    private RegistryOptions? _registryOptions;
    private bool _isUpdating;

    public JsonBodyEditor()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        _editor = this.FindControl<TextEditor>("Editor");
        
        if (_editor != null)
        {
            SetupEditorDefaults();
            SetupTextMate();
            
            _foldingManager = FoldingManager.Install(_editor.TextArea);
            _editor.TextArea.IndentationStrategy = new CSharpIndentationStrategy();
            
            UpdateLanguage();

            _editor.TextArea.TextEntering += OnTextEntering;
            _editor.TextArea.TextEntered += OnTextEntered;
            
            _editor.TextChanged += (_, _) =>
            {
                if (!_isUpdating && _editor.Document != null)
                {
                    _isUpdating = true;
                    SetValue(TextProperty, _editor.Document.Text);
                    _isUpdating = false;
                    UpdateFoldings();
                }
            };
            
            _editor.Loaded += OnEditorLoaded;
        }
    }

    private void SetupEditorDefaults()
    {
        if (_editor == null) return;
        _editor.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        _editor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
        _editor.Document = new TextDocument();
        _editor.Padding = EditorPadding;
        _editor.Margin = EditorMargin;
        _editor.WordWrap = true;
        
        _editor.Options = new TextEditorOptions
        {
            IndentationSize = 2,
            ConvertTabsToSpaces = true,
            AllowScrollBelowDocument = false,
            EnableRectangularSelection = true,
            EnableTextDragDrop = true
        };
        
        _editor.IsReadOnly = IsReadOnly;
    }

    private void SetupTextMate()
    {
        if (_editor == null) return;
        
        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = _editor.InstallTextMate(_registryOptions);
        
        UpdateLanguage();
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_editor == null || e.Text == null || e.Text.Length == 0) return;

        char nextChar = GetNextChar();
        
        // If next character is closing bracket and we are typing the same bracket
        if (e.Text[0] == nextChar && (nextChar == ')' || nextChar == ']' || nextChar == '}' || nextChar == '"'))
        {
            e.Handled = true;
            
            int newOffset = _editor.TextArea.Caret.Offset + 1;
            if (newOffset <= _editor.Document.TextLength)
            {
                _editor.TextArea.Caret.Offset = newOffset;
            }
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_editor == null || e.Text == null || e.Text.Length == 0) return;

        var caret = _editor.TextArea.Caret;
        var document = _editor.Document;
        
        char typedChar = e.Text[0];
        
        // Automatically close brackets and quotes
        if (typedChar != '\n' && typedChar != '\r')
        {
            string? closingChar = typedChar switch
            {
                '{' => "}",
                '[' => "]",
                '(' => ")",
                '"' => "\"",
                _ => null
            };

            if (closingChar != null)
            {
                int offset = caret.Offset;
                
                // Do not close quotes if next character is not space/newline
                if (typedChar == '"')
                {
                    char nextChar = GetNextChar();
                    if (nextChar != ' ' && nextChar != '\0' && nextChar != '\n' && nextChar != '\r')
                    {
                        return;
                    }
                }
                
                if (offset <= document.TextLength)
                {
                    document.Insert(offset, closingChar);
                    if (offset <= document.TextLength)
                    {
                        caret.Offset = offset;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Retrieves the next character at the current caret position within the editor's document.
    /// </summary>
    /// <returns>The character at the current caret position, or '\0' if no character is available or the editor is uninitialized.</returns>
    private char GetNextChar()
    {
        if (_editor == null) return '\0';
        
        int offset = _editor.TextArea.Caret.Offset;
        if (offset < _editor.Document.TextLength)
        {
            return _editor.Document.GetCharAt(offset);
        }
        return '\0';
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
                lineNumbers.Margin = new Thickness(10, 11, 4, 0);
            }
            else if (margin is FoldingMargin foldingMargin)
            {
                foldingMargin.Margin = new Thickness(0, 10, 0, 0);
            }
        }

        _editor.TextArea.TextView.Margin = new Thickness(0, 10, 50, 0);
        _editor.TextArea.TextView.Redraw();
    }
    
    private void UpdateLanguage()
    {
        if (_editor == null || _textMateInstallation == null) return;

        try
        {
            string ext = ContentType?.ToLower() switch
            {
                "xml" => ".xml",
                "html" => ".html",
                _ => ".json"
            };
            
            if (ext == ".xml" || ext == ".html")
                _currentFoldingStrategy = new XmlFoldingStrategy();
            else
                _currentFoldingStrategy = new BraceFoldingStrategy();

            var language = _registryOptions.GetLanguageByExtension(ext);
            if (language != null)
            {
                _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
            }
            UpdateFoldings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TextMate Language Update error: {ex.Message}");
        }
    }
    
    private void UpdateFoldings()
    {
        if (_foldingManager == null || _editor?.Document == null || _currentFoldingStrategy == null) return;

        if (_currentFoldingStrategy is BraceFoldingStrategy bfs)
            bfs.UpdateFoldings(_foldingManager, _editor.Document);
        else if (_currentFoldingStrategy is XmlFoldingStrategy xfs)
            xfs.UpdateFoldings(_foldingManager, _editor.Document);
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == TextProperty && !_isUpdating && _editor?.Document != null)
        {
            var newText = change.GetNewValue<string>() ?? string.Empty;
            
            _isUpdating = true;
            
            try
            {
                _editor.Document.BeginUpdate();
                _editor.Document.Text = newText;
                _editor.Document.EndUpdate();
                
                _editor.TextArea?.TextView?.Redraw();
                UpdateFoldings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JsonBodyEditor update error: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }
        if (change.Property == ContentTypeProperty)
        {
            var newType = change.GetNewValue<string>() ?? "json";
            System.Diagnostics.Debug.WriteLine($"[JsonBodyEditor] ContentType changed: {newType}");
            UpdateLanguage();
        }
    }
}
