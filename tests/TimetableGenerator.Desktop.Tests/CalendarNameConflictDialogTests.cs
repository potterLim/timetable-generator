using System;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class CalendarNameConflictDialogTests
{
    [AvaloniaFact]
    public void ReplaceableConflictOffersBothDestinationChoices()
    {
        CalendarNameConflict conflict = createConflict(
            ECalendarExportProvider.Google,
            ECalendarReplacementAvailability.Available);

        CalendarNameConflictDialog dialog = new CalendarNameConflictDialog(conflict);

        try
        {
            TextBlock description = findRequiredControl<TextBlock>(dialog, "AvailableNameDescription");
            TextBlock unavailableDescription = findRequiredControl<TextBlock>(
                dialog,
                "ReplacementUnavailableDescription");
            Button replaceButton = findRequiredControl<Button>(dialog, "ReplaceButton");
            Button createButton = findRequiredControl<Button>(dialog, "CreateButton");

            Assert.Equal("새로 만들면 \"2026-2학기 시간표 (2)\"로 저장됩니다.", description.Text);
            Assert.True(replaceButton.IsEnabled);
            Assert.False(unavailableDescription.IsVisible);
            Assert.Equal("기존 캘린더 대체", AutomationProperties.GetName(replaceButton));
            Assert.Equal("번호를 붙여 새 캘린더 만들기", AutomationProperties.GetName(createButton));
            Assert.Equal("Google 캘린더의 같은 이름 캘린더 확인", AutomationProperties.GetName(dialog));
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void UnsafeConflictDisablesReplacementWithoutHidingTheSafeChoice()
    {
        CalendarNameConflict conflict = createConflict(
            ECalendarExportProvider.Apple,
            ECalendarReplacementAvailability.Unavailable);

        CalendarNameConflictDialog dialog = new CalendarNameConflictDialog(conflict);

        try
        {
            TextBlock unavailableDescription = findRequiredControl<TextBlock>(
                dialog,
                "ReplacementUnavailableDescription");
            Button replaceButton = findRequiredControl<Button>(dialog, "ReplaceButton");
            Button createButton = findRequiredControl<Button>(dialog, "CreateButton");

            Assert.False(replaceButton.IsEnabled);
            Assert.True(unavailableDescription.IsVisible);
            Assert.Equal("이 캘린더는 안전하게 대체할 수 없습니다.", unavailableDescription.Text);
            Assert.True(createButton.IsEnabled);
            Assert.Equal("Apple 캘린더의 같은 이름 캘린더 확인", AutomationProperties.GetName(dialog));
        }
        finally
        {
            dialog.Close();
        }
    }

    private static CalendarNameConflict createConflict(
        ECalendarExportProvider provider,
        ECalendarReplacementAvailability replacementAvailability)
    {
        return new CalendarNameConflict(
            provider,
            new PlanName("2026-2학기 시간표"),
            new PlanName("2026-2학기 시간표 (2)"),
            replacementAvailability);
    }

    private static TControl findRequiredControl<TControl>(Control root, string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The calendar conflict dialog control is unavailable: "
                    + controlName);
        }

        return controlOrNull;
    }
}
