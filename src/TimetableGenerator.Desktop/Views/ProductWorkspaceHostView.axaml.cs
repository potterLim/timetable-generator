using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView : UserControl
{
    public ProductWorkspaceHostView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
