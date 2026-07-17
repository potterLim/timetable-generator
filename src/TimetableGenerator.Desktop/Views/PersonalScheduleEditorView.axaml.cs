using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class PersonalScheduleEditorView : UserControl
{
    private readonly TextBox mNameInput;

    private readonly ToggleButton mMondayInput;

    private readonly TimePicker mStartTimeInput;

    private readonly TimePicker mEndTimeInput;

    private readonly TextBox mSectionInput;

    private readonly TextBox mInstructorInput;

    private readonly TextBox mLocationInput;

    public PersonalScheduleEditorView()
    {
        AvaloniaXamlLoader.Load(this);
        mNameInput = findRequiredControl<TextBox>("PersonalScheduleNameInput");
        mMondayInput = findRequiredControl<ToggleButton>(
            "PersonalScheduleMondayInput");
        mStartTimeInput = findRequiredControl<TimePicker>(
            "PersonalScheduleStartTimeInput");
        mEndTimeInput = findRequiredControl<TimePicker>(
            "PersonalScheduleEndTimeInput");
        mSectionInput = findRequiredControl<TextBox>(
            "PersonalScheduleSectionInput");
        mInstructorInput = findRequiredControl<TextBox>(
            "PersonalScheduleInstructorInput");
        mLocationInput = findRequiredControl<TextBox>(
            "PersonalScheduleLocationInput");
    }

    internal void focusInitialInput()
    {
        mNameInput.Focus();
        mNameInput.SelectAll();
    }

    internal void focusValidationTarget(
        EPersonalScheduleDraftValidationError validationError)
    {
        Control target;
        switch (validationError)
        {
            case EPersonalScheduleDraftValidationError.TitleRequired:
            case EPersonalScheduleDraftValidationError.TitleInvalid:
                target = mNameInput;
                break;
            case EPersonalScheduleDraftValidationError.DayRequired:
                target = mMondayInput;
                break;
            case EPersonalScheduleDraftValidationError.StartTimeRequired:
            case EPersonalScheduleDraftValidationError.StartTimePrecisionInvalid:
            case EPersonalScheduleDraftValidationError.Overlap:
                target = mStartTimeInput;
                break;
            case EPersonalScheduleDraftValidationError.EndTimeRequired:
            case EPersonalScheduleDraftValidationError.EndTimePrecisionInvalid:
            case EPersonalScheduleDraftValidationError.EndNotAfterStart:
            case EPersonalScheduleDraftValidationError.DurationTooShort:
                target = mEndTimeInput;
                break;
            case EPersonalScheduleDraftValidationError.SectionInvalid:
                target = mSectionInput;
                break;
            case EPersonalScheduleDraftValidationError.InstructorInvalid:
                target = mInstructorInput;
                break;
            case EPersonalScheduleDraftValidationError.LocationInvalid:
                target = mLocationInput;
                break;
            case EPersonalScheduleDraftValidationError.None:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(validationError),
                    validationError,
                    "Unknown personal schedule validation error.");
        }

        focusControl(target);
    }

    private static void focusControl(Control target)
    {
        target.BringIntoView();
        if (target.Focus())
        {
            return;
        }

        Control? focusableDescendantOrNull = target.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(
                static candidate => candidate.Focusable
                    && candidate.IsVisible
                    && candidate.IsEnabled);
        focusableDescendantOrNull?.Focus();
    }

    private TControl findRequiredControl<TControl>(string name)
        where TControl : Control
    {
        TControl? controlOrNull = this.FindControl<TControl>(name);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The personal schedule editor control was not found: " + name);
        }

        return controlOrNull;
    }
}
