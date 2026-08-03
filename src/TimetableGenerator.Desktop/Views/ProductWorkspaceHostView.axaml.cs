using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView : UserControl
{
    public ProductWorkspaceHostView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += onDataContextChanged;
        AttachedToVisualTree += onAttachedToVisualTree;
        DetachedFromVisualTree += onDetachedFromVisualTree;
        AddHandler(KeyDownEvent, onKeyDown, RoutingStrategies.Tunnel);
    }
}
