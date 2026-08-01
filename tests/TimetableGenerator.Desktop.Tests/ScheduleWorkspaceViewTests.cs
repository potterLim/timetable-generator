using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceViewTests
{
    [AvaloniaFact]
    public void ScheduleBoardRendersLateEntriesInsideContinuousTimeAxis()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createScheduleEntry(EDay.Monday, new AcademicPeriod(7)));
        entries.Add(createScheduleEntry(EDay.Tuesday, new AcademicPeriod(8)));
        entries.Add(createScheduleEntry(EDay.Wednesday, new AcademicPeriod(9)));
        entries.Add(createScheduleEntry(EDay.Thursday, new AcademicPeriod(10)));

        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(entries));

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
            ScrollViewer? scrollViewerOrNull = scheduleBoard.FindControl<ScrollViewer>("ScheduleScrollViewer");
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
                    if (AutomationProperties.GetName(scheduleCard)?.Contains("목요일 22:30–23:45", StringComparison.Ordinal) == true)
                    {
                        latestScheduleAccessibleNameOrNull = AutomationProperties.GetName(scheduleCard);
                    }
                }
            }

            Assert.Equal(new ScheduleBoardTimeBoundary(1_050), scheduleBoard.RenderedLayout.TimeAxis.Start);
            Assert.Equal(new ScheduleBoardTimeBoundary(1_440), scheduleBoard.RenderedLayout.TimeAxis.End);
            Assert.Equal(79, boardGrid.RowDefinitions.Count);
            Assert.Contains(7, scheduleRows);
            Assert.Contains(25, scheduleRows);
            Assert.Contains(43, scheduleRows);
            Assert.Contains(61, scheduleRows);
            Assert.Contains("목요일 22:30–23:45", latestScheduleAccessibleNameOrNull);
            Assert.DoesNotContain("교시", latestScheduleAccessibleNameOrNull);
            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);
            assertScheduleUsesOuterFrameWithoutEndBoundary(boardGrid);
            assertBoardUsesAutomaticVerticalScrolling(scheduleBoard, boardGrid, scrollViewer);
            assertStickyHeaderMatchesBoardSurface(scheduleBoard);
            assertDayColumnsAreEqual(boardGrid);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardExtendsThroughSundayWhenSundayIsTheOnlyWeekendDay()
    {
        ScheduleEntry sundayEntry = createScheduleEntry(EDay.Sunday, new AcademicPeriod(2));
        ScheduleBoardPresentation presentation = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { sundayEntry }));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = presentation;

        Window window = new Window();
        window.Width = 900.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
            Assert.NotNull(boardGridOrNull);
            if (boardGridOrNull == null)
            {
                throw new InvalidOperationException("The rendered schedule grid was not found.");
            }

            Grid? stickyDayHeaderGridOrNull = scheduleBoard.FindControl<Grid>("BoardStickyDayHeaderGrid");
            Assert.NotNull(stickyDayHeaderGridOrNull);
            if (stickyDayHeaderGridOrNull == null)
            {
                throw new InvalidOperationException("The sticky schedule day header was not found.");
            }

            List<TextBlock> dayHeaders = stickyDayHeaderGridOrNull.Children.OfType<TextBlock>().ToList();
            Button scheduleCard = Assert.Single(boardGridOrNull.Children.OfType<Button>());
            Border? contextHeaderOrNull = scheduleBoard.FindControl<Border>("BoardContextHeader");

            Assert.Same(presentation.Layout, scheduleBoard.RenderedLayout);
            Assert.NotNull(contextHeaderOrNull);
            Assert.False(contextHeaderOrNull.IsVisible);
            Assert.Equal("주간 시간표", AutomationProperties.GetName(scheduleBoard));
            Assert.Equal(8, boardGridOrNull.ColumnDefinitions.Count);
            assertDayColumnsAreEqual(boardGridOrNull);
            Assert.Contains(dayHeaders, textBlock => textBlock.Text == "토");
            Assert.Contains(dayHeaders, textBlock => textBlock.Text == "일");
            Assert.Equal(7, Grid.GetColumn(scheduleCard));
            Assert.Contains("일요일 10:30–11:45", AutomationProperties.GetName(scheduleCard));
            Assert.Contains(boardGridOrNull, scheduleBoard.PngExportSurface.GetVisualDescendants());
            assertScheduleUsesOuterFrameWithoutEndBoundary(boardGridOrNull);
            assertStickyHeaderMatchesBoardSurface(scheduleBoard);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardHeaderBoundaryMatchesSixDayLayout()
    {
        ScheduleEntry saturdayEntry = createScheduleEntry(EDay.Saturday, new AcademicPeriod(2));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { saturdayEntry }));

        Window window = new Window();
        window.Width = 900.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(6, scheduleBoard.RenderedLayout.DayRange.DayCount);
            Grid boardGrid = Assert.IsType<Grid>(scheduleBoard.FindControl<Grid>("BoardGrid"));
            assertScheduleUsesOuterFrameWithoutEndBoundary(boardGrid);
            assertStickyHeaderMatchesBoardSurface(scheduleBoard);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardLabelsWholeHoursAlongThirtyMinuteGuides()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }));

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
            Assert.NotNull(boardGridOrNull);
            if (boardGridOrNull == null)
            {
                throw new InvalidOperationException("The rendered schedule grid was not found.");
            }

            List<TextBlock> timeLabels = boardGridOrNull.Children
                .OfType<TextBlock>()
                .Where(textBlock =>
                    textBlock.Classes.Contains("schedule-time-label"))
                .ToList();
            List<Border> hourGuides = boardGridOrNull.Children
                .OfType<Border>()
                .Where(border =>
                    border.Classes.Contains("schedule-hour-guide"))
                .ToList();
            List<Border> halfHourGuides = boardGridOrNull.Children
                .OfType<Border>()
                .Where(border =>
                    border.Classes.Contains("schedule-half-hour-guide"))
                .ToList();

            Assert.Equal(2, timeLabels.Count);
            Assert.Equal(2, hourGuides.Count);
            Assert.Single(halfHourGuides);
            Assert.Contains(timeLabels, textBlock => textBlock.Text == "09:00");
            Assert.Contains(timeLabels, textBlock => textBlock.Text == "10:00");
            Assert.DoesNotContain(
                timeLabels,
                textBlock => textBlock.Text == "11:00");
            Assert.DoesNotContain(
                timeLabels,
                textBlock => textBlock.Text?.EndsWith(
                    ":30",
                    StringComparison.Ordinal) == true);
            Assert.Equal(new ScheduleBoardTimeBoundary(510), scheduleBoard.RenderedLayout.TimeAxis.Start);
            Assert.Equal(new ScheduleBoardTimeBoundary(630), scheduleBoard.RenderedLayout.TimeAxis.End);
            Assert.Equal(24, scheduleBoard.RenderedLayout.TimeAxis.IncrementCount);
            Assert.Equal(3, scheduleBoard.RenderedLayout.TimeAxis.GuideTimes.Count);
            Assert.DoesNotContain(
                timeLabels,
                textBlock => textBlock.Text?.Contains(
                    "교시",
                    StringComparison.Ordinal) == true);
            Assert.All(
                timeLabels,
                timeLabel =>
                {
                    Border matchingGuide = Assert.Single(
                        hourGuides,
                        guide => Grid.GetRow(guide)
                            == Grid.GetRow(timeLabel) + 1);
                    assertTimeLabelIsCenteredOnGuide(boardGridOrNull, timeLabel, matchingGuide);
                });
            Assert.All(
                hourGuides,
                hourGuide =>
                {
                    Assert.Equal(0, Grid.GetColumn(hourGuide));
                    Assert.Equal(64.0, hourGuide.Margin.Left);
                });
            Assert.All(
                halfHourGuides,
                halfHourGuide =>
                {
                    Assert.Equal(1, Grid.GetColumn(halfHourGuide));
                    Assert.Equal(new Thickness(0.0), halfHourGuide.Margin);
                });
            Assert.Equal(25, boardGridOrNull.RowDefinitions.Count);
            assertScheduleUsesOuterFrameWithoutEndBoundary(boardGridOrNull);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShortScheduleBoardEndsWithItsActualTimeAxisAndHidesTheScrollbar()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 620.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border boardFrame = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("BoardFrame"));
            Border stickyHeader = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("BoardStickyDayHeaderSurface"));
            Border exportSurface = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("BoardExportSurface"));
            Grid boardGrid = Assert.IsType<Grid>(scheduleBoard.FindControl<Grid>("BoardGrid"));
            ScrollViewer scrollViewer = Assert.IsType<ScrollViewer>(scheduleBoard.FindControl<ScrollViewer>("ScheduleScrollViewer"));
            ScrollBar verticalScrollBar = Assert.Single(scrollViewer.GetVisualDescendants()
                    .OfType<ScrollBar>(),
                scrollBar => scrollBar.Orientation == Orientation.Vertical);

            Assert.Equal(boardFrame.Bounds.Height, scheduleBoard.Bounds.Height, 3);
            Assert.InRange(boardFrame.Bounds.Height - stickyHeader.Bounds.Height - boardGrid.Bounds.Height, 1.5, 2.5);
            Assert.True(boardFrame.Bounds.Height < window.ClientSize.Height, "A short timetable should leave the remaining workspace outside its frame.");
            Assert.False(verticalScrollBar.IsEffectivelyVisible);
            Assert.Equal(new Thickness(0.0, 6.0, 4.0, 6.0), verticalScrollBar.Margin);
            Assert.True(scrollViewer.Extent.Height <= scrollViewer.Viewport.Height + 0.5);
            Assert.Equal(scrollViewer.Viewport.Width, exportSurface.Bounds.Width, 3);
            Assert.Equal(exportSurface.Bounds.Width, stickyHeader.Bounds.Width, 3);
            Assert.Null(scheduleBoard.FindControl<Border>("BoardContentRightBoundary"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardAddsAndRemovesScrollingAsTheWindowHeightChanges()
    {
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    createScheduleEntry(EDay.Monday, new AcademicPeriod(1)),
                    createScheduleEntry(EDay.Wednesday, new AcademicPeriod(3)),
                }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 320.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border boardFrame = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("BoardFrame"));
            ScrollViewer scrollViewer = Assert.IsType<ScrollViewer>(scheduleBoard.FindControl<ScrollViewer>("ScheduleScrollViewer"));
            ScrollBar verticalScrollBar = Assert.Single(scrollViewer.GetVisualDescendants()
                    .OfType<ScrollBar>(),
                scrollBar => scrollBar.Orientation == Orientation.Vertical);

            Assert.True(verticalScrollBar.IsEffectivelyVisible);
            Assert.Equal(new Thickness(0.0, 6.0, 4.0, 6.0), verticalScrollBar.Margin);
            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);
            Assert.Equal(scheduleBoard.Bounds.Height, boardFrame.Bounds.Height, 3);

            scrollViewer.ScrollToEnd();
            Dispatcher.UIThread.RunJobs();

            Assert.True(scrollViewer.Offset.Y > 0.0);

            window.Height = 720.0;
            Dispatcher.UIThread.RunJobs();

            Assert.False(verticalScrollBar.IsEffectivelyVisible);
            Assert.True(scrollViewer.Extent.Height <= scrollViewer.Viewport.Height + 0.5);
            Assert.True(boardFrame.Bounds.Height < window.ClientSize.Height);
            Assert.Equal(scheduleBoard.Bounds.Height, boardFrame.Bounds.Height, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardPositionsTheSamePeriodAtDaySpecificTimes()
    {
        ScheduleEntry mondayEntry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleEntry wednesdayEntry = createScheduleEntry(EDay.Wednesday, new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { mondayEntry, wednesdayEntry }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid boardGrid = Assert.IsType<Grid>(scheduleBoard.FindControl<Grid>("BoardGrid"));
            Button mondayCard = Assert.Single(
                boardGrid.Children.OfType<Button>(),
                button => Grid.GetColumn(button) == 1);
            Button wednesdayCard = Assert.Single(
                boardGrid.Children.OfType<Button>(),
                button => Grid.GetColumn(button) == 3);

            Assert.Equal(new ScheduleBoardTimeBoundary(450), scheduleBoard.RenderedLayout.TimeAxis.Start);
            Assert.Equal(19, Grid.GetRow(mondayCard));
            Assert.Equal(13, Grid.GetRow(wednesdayCard));
            Assert.Contains("월요일 09:00–10:15", AutomationProperties.GetName(mondayCard));
            Assert.Contains("수요일 08:30–09:45", AutomationProperties.GetName(wednesdayCard));
        }
        finally
        {
            window.Close();
        }
    }

}
