using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class PersonalScheduleEditorView : UserControl
{
    public PersonalScheduleEditorView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
