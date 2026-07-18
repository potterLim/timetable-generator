using System.Linq;

using Avalonia.Controls;
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
        RadioButton? selectedPreferenceButtonOrNull = this.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault(
                static candidate => candidate.Classes.Contains("preference-choice")
                    && candidate.IsChecked == true);
        if (selectedPreferenceButtonOrNull != null
            && selectedPreferenceButtonOrNull.Focus())
        {
            return;
        }

        RadioButton? firstPreferenceButtonOrNull = this.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault(
                static candidate => candidate.Classes.Contains("preference-choice"));
        if (firstPreferenceButtonOrNull != null
            && firstPreferenceButtonOrNull.Focus())
        {
            return;
        }

        TextBox? alternativeSearchBoxOrNull = this.FindControl<TextBox>(
            "AlternativeCourseSearchBox");
        alternativeSearchBoxOrNull?.Focus();
    }
}
