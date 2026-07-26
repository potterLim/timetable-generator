using System;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;

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
            TextBlock currentNameDescription = findRequiredControl<TextBlock>(
                dialog,
                "CurrentNameDescription");
            TextBlock availableNameDescription = findRequiredControl<TextBlock>(
                dialog,
                "AvailableNameDescription");
            TextBlock unavailableDescription = findRequiredControl<TextBlock>(
                dialog,
                "ReplacementUnavailableDescription");
            Button replaceButton = findRequiredControl<Button>(dialog, "ReplaceButton");
            Button createButton = findRequiredControl<Button>(dialog, "CreateButton");

            Assert.Equal(
                "현재 이름: \"2026-2학기 시간표\"",
                currentNameDescription.Text);
            Assert.Equal(
                "새 이름: \"2026-2학기 시간표 (2)\"",
                availableNameDescription.Text);
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

    [AvaloniaFact]
    public void LongNamesRemainInsideTheShownDialogInEveryProductTheme()
    {
        string requestedName = "긴 한글 English 괄호 ("
            + new string('가', 57)
            + ")";
        string availableName = "Long English 한국어 ("
            + new string('B', 57)
            + ")";
        CalendarNameConflict conflict = new CalendarNameConflict(
            ECalendarExportProvider.Google,
            new PlanName(requestedName),
            new PlanName(availableName),
            ECalendarReplacementAvailability.Available);
        ThemeVariant[] themes =
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
            ThemeVariant.Default,
        };

        foreach (ThemeVariant theme in themes)
        {
            CalendarNameConflictDialog dialog =
                new CalendarNameConflictDialog(conflict);
            dialog.RequestedThemeVariant = theme;
            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            try
            {
                TextBlock currentNameDescription =
                    findRequiredControl<TextBlock>(
                        dialog,
                        "CurrentNameDescription");
                TextBlock availableNameDescription =
                    findRequiredControl<TextBlock>(
                        dialog,
                        "AvailableNameDescription");
                Button createButton = findRequiredControl<Button>(
                    dialog,
                    "CreateButton");

                Assert.True(dialog.ClientSize.Width <= 460.0);
                Assert.True(
                    currentNameDescription.Bounds.Right
                        <= dialog.ClientSize.Width - 24.0);
                Assert.True(
                    availableNameDescription.Bounds.Right
                        <= dialog.ClientSize.Width - 24.0);
                Assert.True(currentNameDescription.Bounds.Height > 21.0);
                Assert.True(availableNameDescription.Bounds.Height > 21.0);
                Assert.True(createButton.IsFocused);
            }
            finally
            {
                dialog.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task EscapeCancelsTheShownDialogAsync()
    {
        Window owner = new Window();
        owner.Show();
        CalendarNameConflictDialog dialog = new CalendarNameConflictDialog(
            createConflict(
                ECalendarExportProvider.Apple,
                ECalendarReplacementAvailability.Available));

        try
        {
            Task<ECalendarNameConflictResolution> resultTask =
                dialog.ShowDialog<ECalendarNameConflictResolution>(owner);
            Dispatcher.UIThread.RunJobs();
            KeyEventArgs escapeEvent = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
            };

            dialog.RaiseEvent(escapeEvent);
            Dispatcher.UIThread.RunJobs();

            Assert.True(escapeEvent.Handled);
            Assert.Equal(
                ECalendarNameConflictResolution.Cancel,
                await resultTask);
        }
        finally
        {
            if (dialog.IsVisible)
            {
                dialog.Close();
            }

            owner.Close();
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
