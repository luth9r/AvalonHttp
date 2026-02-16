using AvalonHttp.ViewModels.EnvironmentAggregate;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AvalonHttp.Views.Components;

public partial class RequestPanelView : UserControl
{
    public static readonly StyledProperty<EnvironmentsViewModel> EnvironmentsViewModelProperty =
        AvaloniaProperty.Register<RequestPanelView, EnvironmentsViewModel>(
            nameof(EnvironmentsViewModel));
    
    public EnvironmentsViewModel EnvironmentsViewModel
    {
        get => GetValue(EnvironmentsViewModelProperty);
        set => SetValue(EnvironmentsViewModelProperty, value);
    }
    
    public RequestPanelView()
    {
        InitializeComponent();
    }
}