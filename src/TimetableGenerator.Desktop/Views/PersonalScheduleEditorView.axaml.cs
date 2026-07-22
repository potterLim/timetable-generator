using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class PersonalScheduleEditorView : UserControl
{
    private const int WEEKDAY_COLUMN_COUNT = 7;

    private readonly TextBox mNameInput;

    private readonly ProductTimePicker mStartTimeInput;

    private readonly ProductTimePicker mEndTimeInput;

    private readonly TextBox mSectionInput;

    private readonly TextBox mInstructorInput;

    private readonly TextBox mLocationInput;

    public PersonalScheduleEditorView()
    {
        AvaloniaXamlLoader.Load(this);
        mNameInput = findRequiredControl<TextBox>("PersonalScheduleNameInput");
        mStartTimeInput = findRequiredControl<ProductTimePicker>("PersonalScheduleStartTimeInput");
        mEndTimeInput = findRequiredControl<ProductTimePicker>("PersonalScheduleEndTimeInput");
        mSectionInput = findRequiredControl<TextBox>("PersonalScheduleSectionInput");
        mInstructorInput = findRequiredControl<TextBox>("PersonalScheduleInstructorInput");
        mLocationInput = findRequiredControl<TextBox>("PersonalScheduleLocationInput");
    }

    internal void focusInitialInput()
    {
        mNameInput.Focus();
        mNameInput.SelectAll();
    }

    internal void focusValidationTarget(EPersonalScheduleDraftValidationError validationError)
    {
        Control target;
        switch (validationError)
        {
            case EPersonalScheduleDraftValidationError.TitleRequired:
            case EPersonalScheduleDraftValidationError.TitleInvalid:
                target = mNameInput;
                break;
            case EPersonalScheduleDraftValidationError.DayRequired:
                target = findDayInput(EDay.Monday);
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

    private void onSavePersonalScheduleButtonClick(object? senderOrNull, RoutedEventArgs eventArguments)
    {
        commitTextInput(mNameInput);
        commitTextInput(mSectionInput);
        commitTextInput(mInstructorInput);
        commitTextInput(mLocationInput);
    }

    private void onDayOptionContainerPrepared(
        object? senderOrNull,
        ContainerPreparedEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= WEEKDAY_COLUMN_COUNT)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventArgs),
                eventArgs.Index,
                "The personal schedule day container index is outside the week.");
        }

        Grid.SetColumn(eventArgs.Container, eventArgs.Index);
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

    private static void commitTextInput(TextBox input)
    {
        BindingExpressionBase? bindingOrNull = BindingOperations.GetBindingExpressionBase(
            input,
            TextBox.TextProperty);
        if (bindingOrNull == null)
        {
            throw new InvalidOperationException("The personal schedule text input requires a two-way binding: " + input.Name);
        }

        bindingOrNull.UpdateSource();
    }

    private ToggleButton findDayInput(EDay day)
    {
        ToggleButton? dayInputOrNull = this.GetVisualDescendants()
            .OfType<ToggleButton>()
            .FirstOrDefault(
                candidate => candidate.DataContext
                    is PersonalScheduleDayOption dayOption
                    && dayOption.Day == day);
        if (dayInputOrNull == null)
        {
            throw new InvalidOperationException("The personal schedule day input was not found: " + day);
        }

        return dayInputOrNull;
    }

    private TControl findRequiredControl<TControl>(string name)
        where TControl : Control
    {
        TControl? controlOrNull = this.FindControl<TControl>(name);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The personal schedule editor control was not found: " + name);
        }

        return controlOrNull;
    }
}
