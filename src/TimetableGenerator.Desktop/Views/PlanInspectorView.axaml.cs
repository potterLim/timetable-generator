using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class PlanInspectorView : UserControl
{
    public PlanInspectorView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
