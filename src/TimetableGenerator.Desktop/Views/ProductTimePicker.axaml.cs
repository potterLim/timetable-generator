using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductTimePicker : UserControl
{
    private const int HOURS_PER_HALF_DAY = TimePickerHourOption.MAXIMUM_VALUE;

    private const string DEFAULT_ACCESSIBLE_CONTEXT_NAME = "시간";

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Avalonia requires the {PropertyName}Property field convention.")]
    public static readonly StyledProperty<ScheduleTime?> SelectedTimeOrNullProperty = AvaloniaProperty.Register<ProductTimePicker, ScheduleTime?>(nameof(SelectedTimeOrNull), defaultBindingMode: BindingMode.TwoWay);

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Avalonia requires the {PropertyName}Property field convention.")]
    public static readonly StyledProperty<string> AccessibleContextNameProperty = AvaloniaProperty.Register<ProductTimePicker, string>(nameof(AccessibleContextName), DEFAULT_ACCESSIBLE_CONTEXT_NAME);

    private static readonly IReadOnlyList<TimePickerMeridiemOption> MERIDIEM_OPTIONS = createMeridiemOptions();

    private static readonly IReadOnlyList<TimePickerHourOption> HOUR_OPTIONS = createHourOptions();

    private static readonly IReadOnlyList<TimePickerMinuteOption> MINUTE_OPTIONS = createMinuteOptions();

    private readonly ComboBox mMeridiemInput;

    private readonly ComboBox mHourInput;

    private readonly ComboBox mMinuteInput;

    private bool mIsSynchronizingSelection;

    public ScheduleTime? SelectedTimeOrNull
    {
        get
        {
            return GetValue(SelectedTimeOrNullProperty);
        }
        set
        {
            SetValue(SelectedTimeOrNullProperty, value);
        }
    }

    public string AccessibleContextName
    {
        get
        {
            return GetValue(AccessibleContextNameProperty);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Time picker accessibility context cannot be empty.", nameof(value));
            }

            SetValue(AccessibleContextNameProperty, value.Trim());
        }
    }

    public ProductTimePicker()
    {
        AvaloniaXamlLoader.Load(this);
        mMeridiemInput = findRequiredControl<ComboBox>("MeridiemInput");
        mHourInput = findRequiredControl<ComboBox>("HourInput");
        mMinuteInput = findRequiredControl<ComboBox>("MinuteInput");

        mMeridiemInput.ItemsSource = MERIDIEM_OPTIONS;
        mHourInput.ItemsSource = HOUR_OPTIONS;
        mMinuteInput.ItemsSource = MINUTE_OPTIONS;

        mMeridiemInput.SelectionChanged += onSelectionChanged;
        mHourInput.SelectionChanged += onSelectionChanged;
        mMinuteInput.SelectionChanged += onSelectionChanged;
        updateSegmentAutomationNames();
        applySelectedTime(SelectedTimeOrNull);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedTimeOrNullProperty)
        {
            applySelectedTime(change.GetNewValue<ScheduleTime?>());
        }

        if (change.Property == AccessibleContextNameProperty)
        {
            updateSegmentAutomationNames();
        }
    }

    private void onSelectionChanged(object? senderOrNull, SelectionChangedEventArgs eventArguments)
    {
        if (mIsSynchronizingSelection)
        {
            return;
        }

        TimePickerMeridiemOption? meridiemOrNull = mMeridiemInput.SelectedItem as TimePickerMeridiemOption;
        TimePickerHourOption? hourOrNull = mHourInput.SelectedItem as TimePickerHourOption;
        TimePickerMinuteOption? minuteOrNull = mMinuteInput.SelectedItem as TimePickerMinuteOption;
        if (meridiemOrNull == null || hourOrNull == null || minuteOrNull == null)
        {
            SetCurrentValue(SelectedTimeOrNullProperty, null);
            return;
        }

        int hour = findTwentyFourHourValue(meridiemOrNull, hourOrNull);
        SetCurrentValue(SelectedTimeOrNullProperty, new ScheduleTime(hour, minuteOrNull.Value));
    }

    private void applySelectedTime(ScheduleTime? selectedTimeOrNull)
    {
        if (mMeridiemInput == null || mHourInput == null || mMinuteInput == null)
        {
            return;
        }

        mIsSynchronizingSelection = true;
        try
        {
            if (selectedTimeOrNull.HasValue == false)
            {
                clearSelection();
                return;
            }

            ScheduleTime selectedTime = selectedTimeOrNull.Value;
            ETimePickerMeridiem meridiem = selectedTime.Hour < HOURS_PER_HALF_DAY ? ETimePickerMeridiem.AnteMeridiem : ETimePickerMeridiem.PostMeridiem;
            int twelveHourValue = selectedTime.Hour % HOURS_PER_HALF_DAY;
            if (twelveHourValue == 0)
            {
                twelveHourValue = HOURS_PER_HALF_DAY;
            }

            mMeridiemInput.SelectedItem = findMeridiemOption(meridiem);
            mHourInput.SelectedItem = findHourOptionOrNull(twelveHourValue);
            mMinuteInput.SelectedItem = findMinuteOptionOrNull(selectedTime.Minute);
        }
        finally
        {
            mIsSynchronizingSelection = false;
        }
    }

    private void clearSelection()
    {
        mMeridiemInput.SelectedItem = null;
        mHourInput.SelectedItem = null;
        mMinuteInput.SelectedItem = null;
    }

    private void updateSegmentAutomationNames()
    {
        if (mMeridiemInput == null || mHourInput == null || mMinuteInput == null)
        {
            return;
        }

        string contextName = AccessibleContextName;
        AutomationProperties.SetName(mMeridiemInput, contextName + " 오전 또는 오후");
        AutomationProperties.SetName(mHourInput, contextName + " 시");
        AutomationProperties.SetName(mMinuteInput, contextName + " 분");
    }

    private static int findTwentyFourHourValue(TimePickerMeridiemOption meridiem, TimePickerHourOption hour)
    {
        int normalizedHour = hour.Value % HOURS_PER_HALF_DAY;
        switch (meridiem.Meridiem)
        {
            case ETimePickerMeridiem.AnteMeridiem:
                return normalizedHour;
            case ETimePickerMeridiem.PostMeridiem:
                return normalizedHour + HOURS_PER_HALF_DAY;
            default:
                throw new ArgumentOutOfRangeException(nameof(meridiem), meridiem.Meridiem, "Unknown time picker meridiem.");
        }
    }

    private static TimePickerMeridiemOption findMeridiemOption(ETimePickerMeridiem meridiem)
    {
        foreach (TimePickerMeridiemOption option in MERIDIEM_OPTIONS)
        {
            if (option.Meridiem == meridiem)
            {
                return option;
            }
        }

        throw new InvalidOperationException("The time picker meridiem option is missing.");
    }

    private static TimePickerHourOption? findHourOptionOrNull(int value)
    {
        foreach (TimePickerHourOption option in HOUR_OPTIONS)
        {
            if (option.Value == value)
            {
                return option;
            }
        }

        return null;
    }

    private static TimePickerMinuteOption? findMinuteOptionOrNull(int value)
    {
        foreach (TimePickerMinuteOption option in MINUTE_OPTIONS)
        {
            if (option.Value == value)
            {
                return option;
            }
        }

        return null;
    }

    private static IReadOnlyList<TimePickerMeridiemOption> createMeridiemOptions()
    {
        return Array.AsReadOnly(
            new TimePickerMeridiemOption[]
            {
                new TimePickerMeridiemOption(ETimePickerMeridiem.AnteMeridiem),
                new TimePickerMeridiemOption(ETimePickerMeridiem.PostMeridiem),
            });
    }

    private static IReadOnlyList<TimePickerHourOption> createHourOptions()
    {
        List<TimePickerHourOption> options = new List<TimePickerHourOption>();
        for (int hour = TimePickerHourOption.MINIMUM_VALUE; hour <= TimePickerHourOption.MAXIMUM_VALUE; ++hour)
        {
            options.Add(new TimePickerHourOption(hour));
        }

        return options.AsReadOnly();
    }

    private static IReadOnlyList<TimePickerMinuteOption> createMinuteOptions()
    {
        List<TimePickerMinuteOption> options = new List<TimePickerMinuteOption>();
        for (int minute = 0; minute < TimePickerMinuteOption.MINUTES_PER_HOUR; minute += TimePickerMinuteOption.MINUTE_INCREMENT_MINUTES)
        {
            options.Add(new TimePickerMinuteOption(minute));
        }

        return options.AsReadOnly();
    }

    private TControl findRequiredControl<TControl>(string name)
        where TControl : Control
    {
        TControl? controlOrNull = this.FindControl<TControl>(name);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The product time picker control was not found: " + name);
        }

        return controlOrNull;
    }
}
