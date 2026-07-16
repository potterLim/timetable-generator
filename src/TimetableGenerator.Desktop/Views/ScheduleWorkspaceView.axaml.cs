using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView : UserControl
{
    public ScheduleWorkspaceView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
