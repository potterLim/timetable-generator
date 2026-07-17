using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class CourseChoiceEditorView : UserControl
{
    public CourseChoiceEditorView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    internal void focusInitialInput()
    {
        ToggleButton? firstPreferenceButtonOrNull = this.GetVisualDescendants()
            .OfType<ToggleButton>()
            .FirstOrDefault(
                static candidate => candidate.Classes.Contains("preference-choice"));
        if (firstPreferenceButtonOrNull != null
            && firstPreferenceButtonOrNull.Focus(NavigationMethod.Tab))
        {
            return;
        }

        TextBox? alternativeSearchBoxOrNull = this.FindControl<TextBox>(
            "AlternativeCourseSearchBox");
        alternativeSearchBoxOrNull?.Focus(NavigationMethod.Tab);
    }
}
