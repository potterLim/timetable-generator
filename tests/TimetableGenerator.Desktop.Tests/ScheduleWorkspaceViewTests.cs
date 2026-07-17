using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleWorkspaceViewTests
{
    private static readonly ColorToken BORDER =
        new ColorToken("BorderBrush");
    private static readonly ColorToken TEXT_SECONDARY =
        new ColorToken("TextSecondaryBrush");
    private static readonly ColorToken ACCENT =
        new ColorToken("AccentBrush");

    [AvaloniaFact]
    public void ScheduleBoardRendersLateEntriesInsideContinuousTimeAxis()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createScheduleEntry(EDay.Monday, new AcademicPeriod(7)));
        entries.Add(createScheduleEntry(EDay.Tuesday, new AcademicPeriod(8)));
        entries.Add(createScheduleEntry(EDay.Wednesday, new AcademicPeriod(9)));
        entries.Add(createScheduleEntry(EDay.Thursday, new AcademicPeriod(10)));

        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(entries));

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
                    if (AutomationProperties.GetName(scheduleCard)?.Contains(
                        "목요일 22:00–23:15",
                        StringComparison.Ordinal) == true)
                    {
                        latestScheduleAccessibleNameOrNull =
                            AutomationProperties.GetName(scheduleCard);
                    }
                }
            }

            Assert.Equal(
                new ScheduleBoardTimeBoundary(510),
                scheduleBoard.RenderedLayout.TimeAxis.Start);
            Assert.Equal(
                new ScheduleBoardTimeBoundary(1_410),
                scheduleBoard.RenderedLayout.TimeAxis.End);
            Assert.Equal(181, boardGrid.RowDefinitions.Count);
            Assert.Contains(109, scheduleRows);
            Assert.Contains(127, scheduleRows);
            Assert.Contains(145, scheduleRows);
            Assert.Contains(163, scheduleRows);
            Assert.Contains(
                "목요일 22:00–23:15",
                latestScheduleAccessibleNameOrNull);
            Assert.DoesNotContain("교시", latestScheduleAccessibleNameOrNull);
            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardExtendsThroughSundayWhenSundayIsTheOnlyWeekendDay()
    {
        ScheduleEntry sundayEntry = createScheduleEntry(
            EDay.Sunday,
            new AcademicPeriod(2));
        ScheduleBoardPresentation presentation = createScheduleBoardPresentation(
            new ScheduleRecommendation(new ScheduleEntry[] { sundayEntry }));
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
                throw new InvalidOperationException(
                    "The rendered schedule grid was not found.");
            }

            List<TextBlock> dayHeaders = boardGridOrNull.Children
                .OfType<TextBlock>()
                .Where(textBlock => Grid.GetRow(textBlock) == 0)
                .ToList();
            Button scheduleCard = Assert.Single(
                boardGridOrNull.Children.OfType<Button>());

            Assert.Same(presentation.Layout, scheduleBoard.RenderedLayout);
            Assert.Equal(8, boardGridOrNull.ColumnDefinitions.Count);
            Assert.Contains(dayHeaders, textBlock => textBlock.Text == "토");
            Assert.Contains(dayHeaders, textBlock => textBlock.Text == "일");
            Assert.Equal(7, Grid.GetColumn(scheduleCard));
            Assert.Contains(
                "일요일 10:00–11:15",
                AutomationProperties.GetName(scheduleCard));
            Assert.Contains(
                boardGridOrNull,
                scheduleBoard.PngExportSurface.GetVisualDescendants());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardLabelsEveryHalfHourWithActualTimes()
    {
        ScheduleEntry entry = createScheduleEntry(
            EDay.Monday,
            new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(new ScheduleEntry[] { entry }));

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
                throw new InvalidOperationException(
                    "The rendered schedule grid was not found.");
            }

            List<TextBlock> timeLabels = boardGridOrNull.Children
                .OfType<TextBlock>()
                .Where(textBlock => Grid.GetColumn(textBlock) == 0
                    && Grid.GetRow(textBlock) > 0)
                .ToList();

            Assert.Equal(21, timeLabels.Count);
            Assert.Contains(timeLabels, textBlock => textBlock.Text == "08:30");
            Assert.Contains(timeLabels, textBlock => textBlock.Text == "09:00");
            Assert.Contains(timeLabels, textBlock => textBlock.Text == "18:30");
            Assert.DoesNotContain(
                timeLabels,
                textBlock => textBlock.Text?.Contains(
                    "교시",
                    StringComparison.Ordinal) == true);
            Assert.Equal(127, boardGridOrNull.RowDefinitions.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleCardKeepsCourseNameOnOneLineAndOpensCompleteDetails()
    {
        const string LONG_NAME = "사용자 경험과 인터페이스 설계를 위한 고급 프로젝트 실습";
        const string LONG_INSTRUCTOR = "김테스트, 박테스트 외 3명";
        const string LONG_LOCATION = "느헤미야홀 401호 공동 프로젝트 스튜디오";

        ScheduleEntry entry = createLongScheduleEntry(
            EDay.Monday,
            new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(new ScheduleEntry[] { entry }));

        Window window = new Window();
        window.Width = 660.0;
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
                throw new InvalidOperationException("The schedule board grid was not found.");
            }

            Button? scheduleCardOrNull = null;
            foreach (Control child in boardGridOrNull.Children)
            {
                if (child is Button renderedScheduleCard)
                {
                    scheduleCardOrNull = renderedScheduleCard;
                    break;
                }
            }

            Assert.NotNull(scheduleCardOrNull);
            if (scheduleCardOrNull == null)
            {
                throw new InvalidOperationException("The rendered schedule card was not found.");
            }

            Button scheduleCard = scheduleCardOrNull;
            Assert.Contains(LONG_NAME, AutomationProperties.GetName(scheduleCard));
            Assert.Equal(
                "선택하면 과목의 전체 시간, 담당교원, 강의실 정보를 엽니다.",
                AutomationProperties.GetHelpText(scheduleCard));

            List<TextBlock> cardTexts = scheduleCard.GetVisualDescendants()
                .OfType<TextBlock>()
                .ToList();
            TextBlock courseName = Assert.Single(
                cardTexts,
                textBlock => textBlock.Text == LONG_NAME);
            Assert.Equal(TextWrapping.NoWrap, courseName.TextWrapping);
            Assert.Equal(TextTrimming.CharacterEllipsis, courseName.TextTrimming);
            Assert.Contains(cardTexts, textBlock => textBlock.Text == LONG_INSTRUCTOR);
            Assert.Contains(cardTexts, textBlock => textBlock.Text == LONG_LOCATION);
            Assert.Equal(
                LONG_NAME + Environment.NewLine + "선택하여 과목 상세 정보 보기",
                ToolTip.GetTip(scheduleCard));
            Assert.Equal(15, Grid.GetRowSpan(scheduleCard));

            FlyoutBase? detailsFlyoutOrNull = scheduleCard.Flyout;
            Assert.NotNull(detailsFlyoutOrNull);
            if (detailsFlyoutOrNull == null)
            {
                throw new InvalidOperationException("The schedule detail flyout was not found.");
            }

            bool hasFocusedCard = scheduleCard.Focus();
            Assert.True(hasFocusedCard);
            window.KeyPress(
                Key.Enter,
                RawInputModifiers.None,
                PhysicalKey.Enter,
                null);
            window.KeyRelease(
                Key.Enter,
                RawInputModifiers.None,
                PhysicalKey.Enter,
                null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(detailsFlyoutOrNull.IsOpen);
            detailsFlyoutOrNull.Hide();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardRebuildsCodeGeneratedBrushesWhenThemeChanges()
    {
        ScheduleEntry entry = createScheduleEntry(
            EDay.Monday,
            new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(new ScheduleEntry[] { entry }));

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.RequestedThemeVariant = ThemeVariant.Light;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            RenderedScheduleBrushes lightBrushes = findRenderedScheduleBrushes(
                scheduleBoard);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();

            RenderedScheduleBrushes darkBrushes = findRenderedScheduleBrushes(
                scheduleBoard);
            SolidColorBrush expectedDarkBorder = findRequiredThemeBrush(
                BORDER,
                ThemeVariant.Dark);
            SolidColorBrush expectedDarkSecondary = findRequiredThemeBrush(
                TEXT_SECONDARY,
                ThemeVariant.Dark);
            SolidColorBrush expectedDarkAccent = findRequiredThemeBrush(
                ACCENT,
                ThemeVariant.Dark);

            Assert.NotSame(lightBrushes.ScheduleCard, darkBrushes.ScheduleCard);
            Assert.NotEqual(
                lightBrushes.CellBorder.Color,
                darkBrushes.CellBorder.Color);
            Assert.NotEqual(
                lightBrushes.TimeLabel.Color,
                darkBrushes.TimeLabel.Color);
            Assert.NotEqual(
                lightBrushes.DetailAccent.Color,
                darkBrushes.DetailAccent.Color);
            Assert.Equal(expectedDarkBorder.Color, darkBrushes.CellBorder.Color);
            Assert.Equal(expectedDarkSecondary.Color, darkBrushes.TimeLabel.Color);
            Assert.Equal(expectedDarkAccent.Color, darkBrushes.DetailAccent.Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PngExportSurfaceIncludesEveryPeriodAndExpandedCardContentAsync()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createLongScheduleEntry(EDay.Thursday, new AcademicPeriod(10)));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(entries));

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ScrollViewer? scrollViewerOrNull = scheduleBoard.FindControl<ScrollViewer>(
                "ScheduleScrollViewer");
            Assert.NotNull(scrollViewerOrNull);
            if (scrollViewerOrNull == null)
            {
                throw new InvalidOperationException(
                    "The rendered schedule scroll viewer was not found.");
            }

            Assert.True(
                scheduleBoard.PngExportSurface.Bounds.Height
                    > scrollViewerOrNull.Viewport.Height);
            Assert.True(scheduleBoard.PngExportSurface.Bounds.Height > 1_002.0);

            AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(
                PngExportScale.PRODUCT_QUALITY);
            using (MemoryStream destinationStream = new MemoryStream())
            {
                await exporter.ExportControlAsync(
                    scheduleBoard.PngExportSurface,
                    destinationStream,
                    CancellationToken.None);
                destinationStream.Position = 0L;
                using (Bitmap bitmap = new Bitmap(destinationStream))
                {
                    Assert.True(bitmap.PixelSize.Height > 2_004);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CancelingPngSaveClearsPreviousExportStatusAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
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

            TextBlock? exportStatusOrNull =
                workspaceView.FindControl<TextBlock>("ExportStatusText");
            Assert.NotNull(exportStatusOrNull);
            if (exportStatusOrNull == null)
            {
                throw new InvalidOperationException("The PNG export status was not found.");
            }

            TextBlock exportStatus = exportStatusOrNull;
            exportStatus.Text = "PNG로 저장했습니다.";
            exportStatus.IsVisible = true;
            exportStatus.Classes.Set("success", true);

            AsyncDelegateCommand exportCommand = Assert.IsType<AsyncDelegateCommand>(
                workspaceView.ExportCommand);
            exportCommand.Execute(null);
            await exportCommand.ExecutionTask;
            Dispatcher.UIThread.RunJobs();

            Assert.False(exportStatus.IsVisible);
            Assert.Equal(string.Empty, exportStatus.Text);
            Assert.DoesNotContain("success", exportStatus.Classes);
            Assert.DoesNotContain("error", exportStatus.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExportActionIsAvailableForARenderedScheduleAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
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
            Assert.True(exportButton.IsEnabled);
            Assert.NotNull(exportButton.Command);
            Assert.Equal(
                "현재 시간표를 PNG로 저장",
                AutomationProperties.GetName(exportButton));
            Assert.Equal(
                "현재 추천 시간표를 고해상도 PNG 파일로 저장합니다.",
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

    private static ScheduleBoardPresentation createScheduleBoardPresentation(
        ScheduleRecommendation schedule)
    {
        return new ScheduleBoardPresentation(
            schedule,
            new PlanName("테스트 계획"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"));
    }

    private static ScheduleEntry createScheduleEntry(
        EDay day,
        AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            new ScheduleCourseDetails(
                new CourseCode("TST00100"),
                new KoreanCourseName("저녁 수업"),
                new CourseCredits(3m),
                new ScheduleInstructorSummary("테스트 교수"),
                new ScheduleLocationSummary("테스트 강의실")),
            day,
            period,
            ECourseAccent.Blue);
    }

    private static ScheduleEntry createLongScheduleEntry(
        EDay day,
        AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            new ScheduleCourseDetails(
                new CourseCode("UXD00100"),
                new KoreanCourseName(
                    "사용자 경험과 인터페이스 설계를 위한 고급 프로젝트 실습"),
                new CourseCredits(3m),
                new ScheduleInstructorSummary("김테스트, 박테스트 외 3명"),
                new ScheduleLocationSummary(
                    "느헤미야홀 401호 공동 프로젝트 스튜디오")),
            day,
            period,
            ECourseAccent.Blue);
    }

    private static RenderedScheduleBrushes findRenderedScheduleBrushes(
        ScheduleBoardView scheduleBoard)
    {
        Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
        Assert.NotNull(boardGridOrNull);
        if (boardGridOrNull == null)
        {
            throw new InvalidOperationException(
                "The rendered schedule grid was not found.");
        }

        Border? cellOrNull = null;
        TextBlock? timeLabelOrNull = null;
        Button? scheduleCardOrNull = null;
        foreach (Control child in boardGridOrNull.Children)
        {
            if (cellOrNull == null && child is Border cell && cell.BorderBrush != null)
            {
                cellOrNull = cell;
            }

            if (child is TextBlock timeText
                && timeText.Text != null
                && timeText.Text.Contains("08:30", StringComparison.Ordinal))
            {
                timeLabelOrNull = timeText;
            }

            if (child is Button scheduleCard)
            {
                scheduleCardOrNull = scheduleCard;
            }
        }

        Assert.NotNull(cellOrNull);
        Assert.NotNull(timeLabelOrNull);
        Assert.NotNull(scheduleCardOrNull);
        if (cellOrNull == null ||
            timeLabelOrNull == null ||
            scheduleCardOrNull == null)
        {
            throw new InvalidOperationException(
                "The generated schedule visuals were incomplete.");
        }

        SolidColorBrush cellBorder = Assert.IsType<SolidColorBrush>(
            cellOrNull.BorderBrush);
        SolidColorBrush timeLabel = Assert.IsType<SolidColorBrush>(
            timeLabelOrNull.Foreground);
        Flyout detailsFlyout = Assert.IsType<Flyout>(scheduleCardOrNull.Flyout);
        Border detailsSurface = Assert.IsType<Border>(detailsFlyout.Content);
        StackPanel details = Assert.IsType<StackPanel>(detailsSurface.Child);
        TextBlock identity = Assert.IsType<TextBlock>(details.Children[0]);
        SolidColorBrush detailAccent = Assert.IsType<SolidColorBrush>(
            identity.Foreground);

        return new RenderedScheduleBrushes(
            scheduleCardOrNull,
            cellBorder,
            timeLabel,
            detailAccent);
    }

    private static SolidColorBrush findRequiredThemeBrush(
        ColorToken colorToken,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException(
                "The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            colorToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(
            hasResource,
            "Missing brush resource: " + colorToken.Value);
        return Assert.IsType<SolidColorBrush>(resourceOrNull);
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct RenderedScheduleBrushes(
        Button ScheduleCard,
        SolidColorBrush CellBorder,
        SolidColorBrush TimeLabel,
        SolidColorBrush DetailAccent);
}
