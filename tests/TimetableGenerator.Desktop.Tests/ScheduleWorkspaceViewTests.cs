using System;
using System.Collections.Generic;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Sample;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleWorkspaceViewTests
{
    [AvaloniaFact]
    public void ScheduleBoardRendersEveningPeriodsInsideScrollableViewport()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createScheduleEntry(EAcademicDay.Monday, new AcademicPeriod(7)));
        entries.Add(createScheduleEntry(EAcademicDay.Tuesday, new AcademicPeriod(8)));
        entries.Add(createScheduleEntry(EAcademicDay.Wednesday, new AcademicPeriod(9)));
        entries.Add(createScheduleEntry(EAcademicDay.Thursday, new AcademicPeriod(10)));

        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = new ScheduleRecommendation(entries);

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
            ScrollViewer? scrollViewerOrNull =
                scheduleBoard.FindControl<ScrollViewer>("ScheduleScrollViewer");
            Assert.NotNull(boardGridOrNull);
            Assert.NotNull(scrollViewerOrNull);
            if (boardGridOrNull == null || scrollViewerOrNull == null)
            {
                throw new InvalidOperationException("The rendered schedule controls were not found.");
            }

            Grid boardGrid = boardGridOrNull;
            ScrollViewer scrollViewer = scrollViewerOrNull;
            HashSet<int> scheduleRows = new HashSet<int>();
            string? latestScheduleAccessibleNameOrNull = null;
            foreach (Control child in boardGrid.Children)
            {
                if (child is Button scheduleCard)
                {
                    int scheduleRow = Grid.GetRow(scheduleCard);
                    scheduleRows.Add(scheduleRow);
                    if (scheduleRow == 10)
                    {
                        latestScheduleAccessibleNameOrNull =
                            AutomationProperties.GetName(scheduleCard);
                    }
                }
            }

            Assert.Equal(10, scheduleBoard.RenderedPeriodCount);
            Assert.Equal(11, boardGrid.RowDefinitions.Count);
            Assert.Contains(7, scheduleRows);
            Assert.Contains(8, scheduleRows);
            Assert.Contains(9, scheduleRows);
            Assert.Contains(10, scheduleRows);
            Assert.Contains("목요일 10교시", latestScheduleAccessibleNameOrNull);
            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ExportActionCommunicatesThatItIsNotYetAvailable()
    {
        PlannerWorkspaceViewModel workspace = PlannerSampleStateFactory.CreateWorkspace();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView();
        workspaceView.DataContext = workspace;

        Window window = new Window();
        window.Width = 1_100.0;
        window.Height = 720.0;
        window.Content = workspaceView;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button? exportButtonOrNull =
                workspaceView.FindControl<Button>("ExportScheduleButton");
            Assert.NotNull(exportButtonOrNull);
            if (exportButtonOrNull == null)
            {
                throw new InvalidOperationException("The schedule export action was not found.");
            }

            Button exportButton = exportButtonOrNull;
            Assert.False(exportButton.IsEnabled);
            Assert.Null(exportButton.Command);
            Assert.Equal(
                "현재 시간표를 PNG로 저장",
                AutomationProperties.GetName(exportButton));
            Assert.Equal(
                "내보내기 서비스가 연결되면 사용할 수 있습니다.",
                AutomationProperties.GetHelpText(exportButton));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AcademicPeriodRejectsValuesOutsideSupportedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new AcademicPeriod(0);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new AcademicPeriod(11);
            });
    }

    private static ScheduleEntry createScheduleEntry(
        EAcademicDay day,
        AcademicPeriod period)
    {
        return new ScheduleEntry(
            "TEST100",
            "저녁 수업",
            "테스트 교수",
            "테스트 강의실",
            day,
            period,
            ECourseAccent.Blue);
    }
}
