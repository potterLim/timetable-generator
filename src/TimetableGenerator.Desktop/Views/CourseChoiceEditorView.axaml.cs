using System.Linq;

using Avalonia.Controls;
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
        RadioButton? selectedPreferenceButtonOrNull = this.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault(
                static candidate => candidate.Classes.Contains("preference-choice")
                    && candidate.IsChecked == true);
        if (selectedPreferenceButtonOrNull != null && selectedPreferenceButtonOrNull.Focus(NavigationMethod.Pointer))
        {
            return;
        }

        RadioButton? firstPreferenceButtonOrNull = this.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault(
                static candidate => candidate.Classes.Contains("preference-choice"));
        if (firstPreferenceButtonOrNull != null && firstPreferenceButtonOrNull.Focus(NavigationMethod.Pointer))
        {
            return;
        }

        TextBox? alternativeSearchBoxOrNull = this.FindControl<TextBox>("AlternativeCourseSearchBox");
        alternativeSearchBoxOrNull?.Focus();
    }
}
