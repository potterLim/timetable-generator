using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class AppearanceSettingsView : UserControl
{
    public AppearanceSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
