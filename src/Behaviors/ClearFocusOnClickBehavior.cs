using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using AvalonHttp.ViewModels;
using AvalonHttp.ViewModels.EnvironmentAggregate;

namespace AvalonHttp.Behaviors;

public class ClearFocusOnClickBehavior : Behavior<Control>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        
        if (IsInteractiveControl(e.Source as Visual)) return;

        var topLevel = TopLevel.GetTopLevel(AssociatedObject);
        if (topLevel == null) return;
        
        topLevel.FocusManager?.ClearFocus();

        var context = AssociatedObject.DataContext;

        System.Diagnostics.Debug.WriteLine($"ClearFocusOnClickBehavior: {context?.GetType().Name ?? "null"}");
        if (context is MainWindowViewModel mainVm)
        {
            mainVm.CollectionsWorkspace?.CollectionsViewModel?.CloseAllEditModesCommand.Execute(null);
        }
        else if (context is EnvironmentsViewModel envVm)
        {
            envVm.CloseAllEditModesCommand.Execute(null);
        }
    }

    private bool IsInteractiveControl(Visual? element)
    {
        if (element == null)
            return false;

        var current = element;
        while (current != null)
        {
            if (current is TextBox or ComboBox or AutoCompleteBox or DatePicker or Button or ToggleButton)
                return true;

            if (current.GetType().Name == "TextPresenter") 
                return true;

            current = current.GetVisualParent();
        }

        return false;
    }
}