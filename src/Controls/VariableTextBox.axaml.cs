using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvalonHttp.ViewModels.EnvironmentAggregate;

namespace AvalonHttp.Controls;

public class VariableTextBox : TemplatedControl
{
    private const string RevealedPseudoClass = ":revealed";

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<VariableTextBox, string>(nameof(Text), defaultValue: string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<VariableTextBox, string>(nameof(Watermark), defaultValue: string.Empty);

    public static readonly StyledProperty<EnvironmentsViewModel?> EnvironmentsViewModelProperty =
        AvaloniaProperty.Register<VariableTextBox, EnvironmentsViewModel?>(nameof(EnvironmentsViewModel));
    
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<VariableTextBox, CornerRadius>(nameof(CornerRadius));
    
    public static readonly StyledProperty<char> PasswordCharProperty =
        AvaloniaProperty.Register<VariableTextBox, char>(nameof(PasswordChar));
    
    public static readonly StyledProperty<bool> AcceptsReturnProperty =
        AvaloniaProperty.Register<VariableTextBox, bool>(nameof(AcceptsReturn), defaultValue: false);

    public bool AcceptsReturn
    {
        get => GetValue(AcceptsReturnProperty);
        set => SetValue(AcceptsReturnProperty, value);
    }

    public char PasswordChar
    {
        get => GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }
    
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private TextBox? _textBox;
    private TextBlock? _highlightText;
    private Button? _revealButton;
    private bool _isPasswordRevealed;
    private IBrush? _warningBrush;
    private IBrush? _textPrimaryBrush;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public EnvironmentsViewModel? EnvironmentsViewModel
    {
        get => GetValue(EnvironmentsViewModelProperty);
        set => SetValue(EnvironmentsViewModelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        _warningBrush = Application.Current?.FindResource("Warning") as IBrush;
        _textPrimaryBrush = Application.Current?.FindResource("TextPrimary") as IBrush;

        _textBox = e.NameScope.Find<TextBox>("PART_TextBox");
        _highlightText = e.NameScope.Find<TextBlock>("PART_HighlightText");
        
        _revealButton = e.NameScope.Find<Button>("PART_RevealButton");

        if (_revealButton != null)
        {
            _revealButton.Click += (_, __) => TogglePasswordReveal();
        }

        if (_textBox != null)
        {
            _textBox.PropertyChanged += (s, ev) =>
            {
                if (ev.Property == TextBox.TextProperty)
                {
                    Text = _textBox.Text ?? string.Empty;
                    UpdateHighlights();
                }
            };
            
            _textBox.PointerMoved += OnTextBoxPointerMoved;
            _textBox.PointerExited += OnTextBoxPointerExited;
        }
        
        UpdateRevealState();
        UpdateHighlights();
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            if (_textBox != null && _textBox.Text != Text)
            {
                _textBox.Text = Text;
            }
            UpdateHighlights();
        }
        else if (change.Property == PasswordCharProperty)
        {
            UpdateRevealState();
        }
    }

    private void UpdateHighlights()
    {
        if (_highlightText == null || !_highlightText.IsVisible)
        {
            return;
        }

        _highlightText.Inlines?.Clear();

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var regex = new Regex(@"(\{\{[^}]+\}\})");
        var parts = regex.Split(Text);
        
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            var run = new Run(part)
            {
                Foreground = regex.IsMatch(part) 
                    ? (_warningBrush ?? Brushes.Orange)
                    : (_textPrimaryBrush ?? Brushes.White),
                FontWeight = regex.IsMatch(part) ? FontWeight.Bold : FontWeight.Normal
            };
            _highlightText.Inlines?.Add(run);
        }
    }

    private void OnTextBoxPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_textBox == null || EnvironmentsViewModel == null)
        {
            return;
        }

        bool hasPasswordChar = PasswordChar != default(char);
        bool isPasswordMode = hasPasswordChar && !_isPasswordRevealed;

        if (isPasswordMode)
        {
             ToolTip.SetIsOpen(this, false);
             return;
        }

        var point = e.GetPosition(_textBox);
        var charIndex = GetCharacterIndexFromPoint(point);

        if (charIndex >= 0)
        {
            var variableInfo = GetVariableAtIndex(charIndex);
            if (variableInfo != null)
            {
                var varName = variableInfo.Value.name;
                var rawValue = $"{{{{{varName}}}}}";
                var resolvedValue = EnvironmentsViewModel.ResolveVariables(rawValue);
            
                var displayValue = resolvedValue == rawValue ? "Unresolved" : resolvedValue;
            
                ToolTip.SetTip(this, $"{varName}: {displayValue}");
                ToolTip.SetIsOpen(this, true);
                return;
            }
        }

        ToolTip.SetIsOpen(this, false);
    }

    private void OnTextBoxPointerExited(object? sender, PointerEventArgs e)
    {
        ToolTip.SetIsOpen(this, false);
    }

    private (string name, int start, int end)? GetVariableAtIndex(int charIndex)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return null;
        }

        var regex = new Regex(@"\{\{([^}]+)\}\}");
        var matches = regex.Matches(Text);

        foreach (Match match in matches)
        {
            if (charIndex >= match.Index && charIndex < match.Index + match.Length)
            {
                return (match.Groups[1].Value.Trim(), match.Index, match.Index + match.Length);
            }
        }
        return null;
    }
    
    private int GetCharacterIndexFromPoint(Point point)
    {
        if (_textBox == null || string.IsNullOrEmpty(_textBox.Text))
        {
            return -1;
        }

        double charWidth = 8.0; 
        int index = (int)(point.X / charWidth);
        
        return Math.Clamp(index, 0, _textBox.Text.Length - 1);
    }

    private void TogglePasswordReveal()
    {
        _isPasswordRevealed = !_isPasswordRevealed;
        UpdateRevealState();
    }

    private void UpdateRevealState()
    {
        bool hasPasswordChar = PasswordChar != default(char);
        bool isPasswordMode = hasPasswordChar && !_isPasswordRevealed;
        
        if (_revealButton != null)
        {
            _revealButton.IsVisible = hasPasswordChar;
        }

        if (!hasPasswordChar)
        {
            _isPasswordRevealed = false;
        }

        if (_textBox != null)
        {
            _textBox.PasswordChar = isPasswordMode ? PasswordChar : default(char);
            _textBox.Foreground = isPasswordMode 
                ? (_textPrimaryBrush ?? Brushes.White)
                : Brushes.Transparent;
        }
        
        if (_highlightText != null)
        {
            _highlightText.IsVisible = !isPasswordMode;
        }
        
        PseudoClasses.Set(RevealedPseudoClass, _isPasswordRevealed);

        if (isPasswordMode)
        {
            ToolTip.SetIsOpen(this, false);
        }
        else
        {
            UpdateHighlights();
        }
    }
}
