using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class CourseBrowserView : UserControl
{
    public CourseBrowserView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
