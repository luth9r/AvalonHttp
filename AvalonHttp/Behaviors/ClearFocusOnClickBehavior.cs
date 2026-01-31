using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using AvalonHttp.ViewModels;

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
        if (e.Handled)
            return;
        
        if (IsInteractiveControl(e.Source as Visual))
            return;
        
        if (AssociatedObject is Window window)
        {
            window.Focus();
            
            if (window.DataContext is MainWindowViewModel vm)
            {
                vm.CollectionsViewModel.CloseAllEditModesCommand.Execute(null);
            }
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