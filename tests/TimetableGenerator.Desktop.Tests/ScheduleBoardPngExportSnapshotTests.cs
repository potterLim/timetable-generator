using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleBoardPngExportSnapshotTests
{
    private const double EXPECTED_WEEKEND_EXPORT_SURFACE_MINIMUM_WIDTH = 996.0;

    private enum EScheduleCardKind
    {
        Course,
        Personal,
    }

    [Fact]
    public void PngExportLayoutEndsAtTheFirstWholeHourAfterContent()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { entry });

        Assert.Equal(new ScheduleBoardTimeBoundary(510), layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(660), layout.TimeAxis.End);
        Assert.Equal(30, layout.TimeAxis.IncrementCount);
        Assert.Equal(4, layout.TimeAxis.GuideTimes.Count);
        Assert.Equal("10:30", layout.TimeAxis.GuideTimes[^1].ToString());
        Assert.Equal(2, layout.TimeAxis.LabelTimes.Count);
        Assert.Equal("10:00", layout.TimeAxis.LabelTimes[^1].ToString());
        Assert.DoesNotContain(
            layout.TimeAxis.LabelTimes,
            boundary => boundary.ToString() == "11:00");
    }

    [Fact]
    public void PngExportLayoutKeepsHalfHourContextWhenEntriesBeginLater()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Monday, new AcademicPeriod(5));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { entry });

        Assert.Equal(new ScheduleBoardTimeBoundary(870), layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(1_020), layout.TimeAxis.End);
        Assert.Equal(30, layout.TimeAxis.IncrementCount);
        Assert.Equal(4, layout.TimeAxis.GuideTimes.Count);
        Assert.Equal(2, layout.TimeAxis.LabelTimes.Count);
        Assert.Equal("15:00", layout.TimeAxis.LabelTimes[0].ToString());
        Assert.Equal("16:00", layout.TimeAxis.LabelTimes[^1].ToString());
    }

    [Fact]
    public void PngExportLayoutExtendsSixthPeriodToNextWholeHourBoundary()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Thursday, new AcademicPeriod(6));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { entry });

        Assert.Equal(new ScheduleBoardTimeBoundary(1_080), layout.TimeAxis.End);
        Assert.Equal("17:30", layout.TimeAxis.GuideTimes[^1].ToString());
        Assert.Equal("17:00", layout.TimeAxis.LabelTimes[^1].ToString());
        Assert.Equal(
            3,
            layout.TimeAxis.IncrementCount
                - layout.TimeAxis.FindEndingRowOffset(entry.TimeRange.End));
    }

    [Fact]
    public void PngExportLayoutExtendsTenthPeriodThroughMidnightBoundary()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Monday, new AcademicPeriod(10));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { entry });

        Assert.Equal(
            new DailyTimeRange(
                new ScheduleTime(22, 30),
                new ScheduleTime(23, 45)),
            entry.TimeRange);
        Assert.Equal(new ScheduleBoardTimeBoundary(1_290), layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(1_440), layout.TimeAxis.End);
        Assert.Equal("23:30", layout.TimeAxis.GuideTimes[^1].ToString());
        Assert.Equal("23:00", layout.TimeAxis.LabelTimes[^1].ToString());
        Assert.Equal(
            3,
            layout.TimeAxis.IncrementCount
                - layout.TimeAxis.FindEndingRowOffset(entry.TimeRange.End));
    }

    [Fact]
    public void PngExportLayoutIncludesSaturdayWhenSundayIsTheOnlyWeekendEntry()
    {
        ScheduleEntry sundayEntry = createScheduleEntry(EDay.Sunday, new AcademicPeriod(2));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { sundayEntry });

        Assert.Equal(7, layout.DayRange.DayCount);
        Assert.Equal(EDay.Saturday, layout.DayRange.Days[5].Day);
        Assert.Equal(EDay.Sunday, layout.DayRange.Days[6].Day);
        Assert.Equal(6, layout.DayRange.FindDay(EDay.Saturday).ColumnIndex);
        Assert.Equal(7, layout.DayRange.FindDay(EDay.Sunday).ColumnIndex);
    }

    [AvaloniaFact]
    public void PngSnapshotRecalculatesFromScheduleInsteadOfUsingSourceLayout()
    {
        ScheduleEntry earlierAlternative = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleEntry currentEntry = createScheduleEntry(EDay.Tuesday, new AcademicPeriod(3));
        ScheduleRecommendation currentSchedule = new ScheduleRecommendation(
            new ScheduleEntry[] { currentEntry });
        ScheduleBoardLayout sourceLayoutWithAlternative =
            ScheduleBoardLayout.CreateForEntries(
                new ScheduleEntry[] { earlierAlternative, currentEntry });
        ScheduleBoardView sourceBoard = new ScheduleBoardView();
        sourceBoard.DataContext = new ScheduleBoardPresentation(
            currentSchedule,
            sourceLayoutWithAlternative,
            new PlanName("PNG 내보내기 테스트"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"));
        Canvas exportHost = new Canvas();
        exportHost.IsHitTestVisible = false;
        exportHost.Opacity = 0.0;
        exportHost.ZIndex = -1;
        Grid root = new Grid();
        root.Children.Add(exportHost);
        root.Children.Add(sourceBoard);
        Window window = createWindow(root, ThemeVariant.Light);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new ScheduleBoardTimeBoundary(510), sourceBoard.RenderedLayout.TimeAxis.Start);
            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.Create(exportHost, sourceBoard))
            {
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(new ScheduleBoardTimeBoundary(690), snapshot.Layout.TimeAxis.Start);
                Assert.Equal(new ScheduleBoardTimeBoundary(840), snapshot.Layout.TimeAxis.End);
                Assert.Equal(30, snapshot.Layout.TimeAxis.IncrementCount);
                Assert.Equal(4, snapshot.Layout.TimeAxis.GuideTimes.Count);
                Assert.Equal(2, snapshot.Layout.TimeAxis.LabelTimes.Count);
                Assert.DoesNotContain(snapshot.Layout.TimeAxis.End, snapshot.Layout.TimeAxis.LabelTimes);
                Assert.Single(findBoardGrid(snapshot.Surface).Children.OfType<Button>());

                Border exportHeader = snapshot.Surface.GetVisualDescendants()
                    .OfType<Border>()
                    .Single(border => border.Name == "BoardContextHeader");
                Assert.True(exportHeader.IsVisible);
                TextBlock title = Assert.Single(exportHeader.GetVisualDescendants().OfType<TextBlock>());
                Assert.Equal("PNG 내보내기 테스트", title.Text);
            }

            Assert.Empty(exportHost.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PngSnapshotCanRenderAnotherCandidateWithoutChangingTheBoard()
    {
        ScheduleEntry displayedEntry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleEntry exportedEntry = createScheduleEntry(EDay.Sunday, new AcademicPeriod(2));
        ScheduleBoardView sourceBoard = createSourceBoard(
            new ScheduleEntry[] { displayedEntry });
        ScheduleBoardPresentation exportedPresentation =
            new ScheduleBoardPresentation(
                new ScheduleRecommendation(
                    new ScheduleEntry[] { exportedEntry }),
                new PlanName("PNG 후보 내보내기 테스트"),
                new InstitutionName("한동대학교"),
                AcademicTerm.Parse("2026-2"));
        Canvas exportHost = new Canvas();
        exportHost.IsHitTestVisible = false;
        exportHost.Opacity = 0.0;
        exportHost.ZIndex = -1;
        Grid root = new Grid();
        root.Children.Add(exportHost);
        root.Children.Add(sourceBoard);
        Window window = createWindow(root, ThemeVariant.Light);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.create(
                    exportHost,
                    exportedPresentation,
                    sourceBoard))
            {
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(7, snapshot.Layout.DayRange.DayCount);
                Assert.Equal(new ScheduleBoardTimeBoundary(570), snapshot.Layout.TimeAxis.Start);
                Assert.Same(
                    displayedEntry,
                    Assert.Single(
                        Assert.IsType<ScheduleBoardPresentation>(
                            sourceBoard.DataContext).Schedule.Entries));
            }

            Assert.Empty(exportHost.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PngSnapshotUpdateRecalculatesLayoutForEachCandidate()
    {
        ScheduleEntry firstCandidateEntry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleEntry secondCandidateEntry = createScheduleEntry(EDay.Sunday, new AcademicPeriod(5));
        ScheduleBoardView sourceBoard = createSourceBoard(
            new ScheduleEntry[] { firstCandidateEntry });
        ScheduleBoardPresentation displayedPresentation = Assert.IsType<ScheduleBoardPresentation>(sourceBoard.DataContext);
        ScheduleBoardPresentation secondCandidate =
            new ScheduleBoardPresentation(
                new ScheduleRecommendation(
                    new ScheduleEntry[] { secondCandidateEntry }),
                new PlanName("PNG 후보별 축 테스트"),
                new InstitutionName("한동대학교"),
                AcademicTerm.Parse("2026-2"));
        Canvas exportHost = new Canvas();
        exportHost.IsHitTestVisible = false;
        exportHost.Opacity = 0.0;
        exportHost.ZIndex = -1;
        Grid root = new Grid();
        root.Children.Add(exportHost);
        root.Children.Add(sourceBoard);
        Window window = createWindow(root, ThemeVariant.Light);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.create(
                    exportHost,
                    displayedPresentation,
                    sourceBoard))
            {
                Assert.Equal(5, snapshot.Layout.DayRange.DayCount);
                Assert.Equal(new ScheduleBoardTimeBoundary(510), snapshot.Layout.TimeAxis.Start);
                Assert.Equal(new ScheduleBoardTimeBoundary(660), snapshot.Layout.TimeAxis.End);
                Assert.Equal(
                    new string[] { "09:00", "10:00" },
                    findPngTimeLabelTexts(snapshot.Surface));

                snapshot.update(secondCandidate, sourceBoard);

                Assert.Equal(7, snapshot.Layout.DayRange.DayCount);
                Assert.Equal(new ScheduleBoardTimeBoundary(870), snapshot.Layout.TimeAxis.Start);
                Assert.Equal(new ScheduleBoardTimeBoundary(1_020), snapshot.Layout.TimeAxis.End);
                Assert.Equal(
                    new string[] { "15:00", "16:00" },
                    findPngTimeLabelTexts(snapshot.Surface));
                Assert.DoesNotContain(
                    snapshot.Layout.TimeAxis.End.ToString(),
                    findPngTimeLabelTexts(snapshot.Surface));
                Assert.Same(displayedPresentation, sourceBoard.DataContext);
            }

            Assert.Empty(exportHost.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PngSnapshotUsesTheWednesdayFirstPeriodTimeAsync()
    {
        ScheduleEntry wednesdayEntry = createScheduleEntry(EDay.Wednesday, new AcademicPeriod(1));
        ScheduleBoardView sourceBoard = createSourceBoard(
            new ScheduleEntry[] { wednesdayEntry });
        Canvas exportHost = new Canvas();
        exportHost.IsHitTestVisible = false;
        exportHost.Opacity = 0.0;
        exportHost.ZIndex = -1;
        Grid root = new Grid();
        root.Children.Add(exportHost);
        root.Children.Add(sourceBoard);
        Window window = createWindow(root, ThemeVariant.Light);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.Create(exportHost, sourceBoard))
            {
                Assert.Equal(new ScheduleBoardTimeBoundary(450), snapshot.Layout.TimeAxis.Start);
                Grid exportBoardGrid = findBoardGrid(snapshot.Surface);
                Button exportCard = Assert.Single(exportBoardGrid.Children.OfType<Button>());
                Assert.True(exportBoardGrid.Bounds.Width > 0.0);
                Assert.True(exportBoardGrid.Bounds.Height > 0.0);
                Assert.True(exportCard.Bounds.Width > 0.0);
                Assert.True(exportCard.Bounds.Height > 0.0);
                Grid exportCardContent = Assert.IsType<Grid>(exportCard.Content);
                TextBlock exportCardTitle = Assert.IsType<TextBlock>(exportCardContent.Children[0]);
                Assert.True(exportCardTitle.Bounds.Width > 0.0);
                Assert.True(exportCardTitle.Bounds.Height > 0.0);
                Assert.DoesNotContain(
                    snapshot.Surface.GetVisualDescendants().OfType<Control>(),
                    descendant => descendant.IsMeasureValid == false
                        || descendant.IsArrangeValid == false);
                Assert.Contains("수요일 08:30–09:45", AutomationProperties.GetName(exportCard));

                AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.Create(1.0));
                using (MemoryStream destinationStream = new MemoryStream())
                {
                    await exporter.ExportControlAsync(snapshot.Surface, destinationStream, CancellationToken.None);
                    destinationStream.Position = 0L;
                    using (Bitmap bitmap = new Bitmap(destinationStream))
                    {
                        assertBitmapContainsOpaqueContent(bitmap);
                    }
                }
            }

            Assert.Empty(exportHost.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task NarrowSundayBoardExportsEveryWeekendColumnAtReadableWidthAsync()
    {
        ScheduleEntry sundayEntry = createScheduleEntry(EDay.Sunday, new AcademicPeriod(2));
        ScheduleBoardView sourceBoard = createSourceBoard(
            new ScheduleEntry[] { sundayEntry });
        sourceBoard.Width = 320.0;
        Canvas exportHost = new Canvas();
        exportHost.IsHitTestVisible = false;
        exportHost.Opacity = 0.0;
        exportHost.ZIndex = -1;
        Grid root = new Grid();
        root.Children.Add(exportHost);
        root.Children.Add(sourceBoard);
        Window window = createWindow(root, ThemeVariant.Light);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.Create(exportHost, sourceBoard))
            {
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(7, snapshot.Layout.DayRange.DayCount);
                Assert.True(snapshot.Surface.Bounds.Width >= EXPECTED_WEEKEND_EXPORT_SURFACE_MINIMUM_WIDTH);

                Grid boardGrid = findBoardGrid(snapshot.Surface);
                Assert.Equal(8, boardGrid.ColumnDefinitions.Count);
                assertWeekendHeadersArePresent(boardGrid);
                assertDayColumnsMeetMinimumWidth(boardGrid, 132.0);

                Button exportCard = Assert.Single(boardGrid.Children.OfType<Button>());
                Grid exportCardContent = Assert.IsType<Grid>(exportCard.Content);
                List<TextBlock> exportCardTexts = exportCardContent.Children.OfType<TextBlock>().ToList();
                Assert.Equal(
                    new string[]
                    {
                        "시간표 내보내기 검증(01)",
                        "테스트 강의실",
                        "테스트 교수",
                    },
                    exportCardTexts.Select(textBlock => textBlock.Text));
                Assert.DoesNotContain(
                    exportCardTexts,
                    textBlock => textBlock.Text == "TST00100");
                Assert.DoesNotContain(
                    exportCardTexts,
                    textBlock => textBlock.Text == "3학점");
                Assert.All(
                    exportCardTexts,
                    textBlock => Assert.Equal(
                        TextAlignment.Center,
                        textBlock.TextAlignment));
                Assert.Equal(14.0, exportCardTexts[0].FontSize);
                Assert.Equal(18.0, exportCardTexts[0].LineHeight);
                Assert.Equal(FontWeight.Bold, exportCardTexts[0].FontWeight);
                Assert.Equal(2, exportCardTexts[0].MaxLines);
                Assert.Equal(7.0, exportCardTexts[1].Margin.Top);
                Assert.Equal(14.0, exportCardTexts[1].LineHeight);
                Assert.Equal(FontWeight.SemiBold, exportCardTexts[1].FontWeight);
                Assert.Equal(2.0, exportCardTexts[2].Margin.Top);
                Assert.Equal(12.0, exportCardTexts[2].LineHeight);

                AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.Create(1.0));
                using (MemoryStream destinationStream = new MemoryStream())
                {
                    await exporter.ExportControlAsync(snapshot.Surface, destinationStream, CancellationToken.None);
                    destinationStream.Position = 0L;
                    using (Bitmap bitmap = new Bitmap(destinationStream))
                    {
                        Assert.True(bitmap.PixelSize.Width >= EXPECTED_WEEKEND_EXPORT_SURFACE_MINIMUM_WIDTH);
                        Assert.Equal((int)Math.Ceiling(snapshot.Surface.Bounds.Height), bitmap.PixelSize.Height);
                        assertBitmapContainsOpaqueContent(bitmap);
                    }
                }
            }

            Assert.Empty(exportHost.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ProjectedUnavailableMetadataIsOmittedFromPngExportCardAsync()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(document);
        ApplicationScheduleRecommendation recommendation =
            CatalogProjectionTestFixture.CreateScheduledRecommendation(
                document,
                new CourseId("course-programming"),
                new OfferingId("offering-programming-alternative"));
        ScheduleRecommendation projectedRecommendation =
            ScheduleRecommendationProjector.Project(
                recommendation,
                catalogProjection);
        ScheduleBoardView sourceBoard = createSourceBoard(projectedRecommendation.Entries);
        Canvas exportHost = new Canvas();
        exportHost.IsHitTestVisible = false;
        exportHost.Opacity = 0.0;
        exportHost.ZIndex = -1;
        Grid root = new Grid();
        root.Children.Add(exportHost);
        root.Children.Add(sourceBoard);
        Window window = createWindow(root, ThemeVariant.Light);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.Create(exportHost, sourceBoard))
            {
                Dispatcher.UIThread.RunJobs();

                Grid boardGrid = findBoardGrid(snapshot.Surface);
                Button exportCard = Assert.Single(boardGrid.Children.OfType<Button>());
                Grid exportCardContent = Assert.IsType<Grid>(exportCard.Content);
                List<TextBlock> exportCardTexts = exportCardContent.Children.OfType<TextBlock>().ToList();
                Assert.Equal(
                    new string[] { "프로그래밍 I(02)" },
                    exportCardTexts.Select(textBlock => textBlock.Text));
                Assert.Single(exportCardContent.RowDefinitions);
                Assert.Single(exportCardContent.Children);
                Assert.DoesNotContain(
                    exportCardTexts,
                    textBlock => textBlock.Text == "교수 정보 없음");
                Assert.DoesNotContain(
                    exportCardTexts,
                    textBlock => textBlock.Text == "강의실 미정");

                AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.Create(1.0));
                using (MemoryStream destinationStream = new MemoryStream())
                {
                    await exporter.ExportControlAsync(snapshot.Surface, destinationStream, CancellationToken.None);
                    destinationStream.Position = 0L;
                    using (Bitmap bitmap = new Bitmap(destinationStream))
                    {
                        assertBitmapContainsOpaqueContent(bitmap);
                    }
                }
            }

            Assert.Empty(exportHost.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleCardsUseSubtleInteractionFeedbackInBothModes()
    {
        ScheduleEntry courseEntry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleEntry personalScheduleEntry = createPersonalScheduleEntry();
        ScheduleBoardView scheduleBoard = createSourceBoard(
            new ScheduleEntry[] { courseEntry, personalScheduleEntry });
        Window window = createWindow(scheduleBoard, ThemeVariant.Light);

        try
        {
            window.Show();
            assertScheduleCardInteractionFeedback(
                window,
                scheduleBoard,
                ThemeVariant.Light,
                EScheduleCardKind.Course);
            assertScheduleCardInteractionFeedback(
                window,
                scheduleBoard,
                ThemeVariant.Light,
                EScheduleCardKind.Personal);
            assertScheduleCardInteractionFeedback(
                window,
                scheduleBoard,
                ThemeVariant.Dark,
                EScheduleCardKind.Course);
            assertScheduleCardInteractionFeedback(
                window,
                scheduleBoard,
                ThemeVariant.Dark,
                EScheduleCardKind.Personal);
        }
        finally
        {
            window.Close();
        }
    }

    private static ScheduleBoardView createSourceBoard(IReadOnlyList<ScheduleEntry> entries)
    {
        ScheduleBoardView sourceBoard = new ScheduleBoardView();
        sourceBoard.DataContext = new ScheduleBoardPresentation(
            new ScheduleRecommendation(entries),
            new PlanName("PNG 내보내기 테스트"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"));
        return sourceBoard;
    }

    private static Window createWindow(Control content, ThemeVariant themeVariant)
    {
        Window window = new Window();
        window.Width = 900.0;
        window.Height = 620.0;
        window.RequestedThemeVariant = themeVariant;
        window.Content = content;
        return window;
    }

    private static ScheduleEntry createScheduleEntry(EDay day, AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            new CourseId("course-tst00100"),
            new OfferingId("offering-tst00100-01"),
            new ScheduleCourseDetails(
                new CourseCode("TST00100"),
                new KoreanCourseName("시간표 내보내기 검증"),
                new CourseCredits(3m),
                new ScheduleInstructorSummary(
                    InstructorAssignmentMetadata.CreateConfirmed(
                        new InstructorDisplayText("테스트 교수"),
                        new AdditionalInstructorCount(0))),
                new ScheduleLocationSummary(
                    LocationAssignmentMetadata.CreateAssigned(
                        new ClassroomDisplayText("테스트 강의실")))),
            new CourseSectionCode("01"),
            new MeetingSlot(day, period),
            ECourseAccent.Blue);
    }

    private static ScheduleEntry createPersonalScheduleEntry()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(8, 30), new ScheduleTime(9, 45));
        WeeklyTimeRange weeklyTimeRange = new WeeklyTimeRange(EDay.Tuesday, timeRange);
        PersonalSchedule personalSchedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("상호작용 피드백 검증"),
            new WeeklyTimeRange[] { weeklyTimeRange },
            PersonalScheduleDetails.CreateEmpty());
        return new PersonalScheduleEntry(personalSchedule, weeklyTimeRange);
    }

    private static Grid findBoardGrid(Control surface)
    {
        Grid? boardGridOrNull = surface.GetVisualDescendants()
            .OfType<Grid>()
            .SingleOrDefault(candidate => candidate.Name == "BoardGrid");
        Assert.NotNull(boardGridOrNull);
        if (boardGridOrNull == null)
        {
            throw new InvalidOperationException("The PNG snapshot schedule grid was not rendered.");
        }

        return boardGridOrNull;
    }

    private static IReadOnlyList<string> findPngTimeLabelTexts(Control surface)
    {
        return findBoardGrid(surface).Children
            .OfType<TextBlock>()
            .Where(textBlock =>
                textBlock.Classes.Contains("schedule-time-label"))
            .Select(getTextOrEmpty)
            .ToList()
            .AsReadOnly();
    }

    private static string getTextOrEmpty(TextBlock textBlock)
    {
        if (textBlock.Text == null)
        {
            return string.Empty;
        }

        return textBlock.Text;
    }

    private static void assertWeekendHeadersArePresent(Grid boardGrid)
    {
        List<string?> headerTexts = boardGrid.Children
            .OfType<TextBlock>()
            .Where(textBlock => Grid.GetRow(textBlock) == 0)
            .Select(textBlock => textBlock.Text)
            .ToList();
        Assert.Contains("토", headerTexts);
        Assert.Contains("일", headerTexts);
    }

    private static void assertDayColumnsMeetMinimumWidth(Grid boardGrid, double minimumWidth)
    {
        for (int columnIndex = 1;
            columnIndex < boardGrid.ColumnDefinitions.Count;
            ++columnIndex)
        {
            Assert.True(
                boardGrid.ColumnDefinitions[columnIndex].ActualWidth
                    >= minimumWidth,
                "The PNG export day column was narrower than its readable minimum.");
        }
    }

    private static void assertScheduleCardInteractionFeedback(
        Window window,
        ScheduleBoardView scheduleBoard,
        ThemeVariant themeVariant,
        EScheduleCardKind cardKind)
    {
        window.RequestedThemeVariant = themeVariant;
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        Grid boardGrid = findBoardGrid(scheduleBoard.PngExportSurface);
        string cardClass;
        string backgroundResourceKey;
        switch (cardKind)
        {
            case EScheduleCardKind.Course:
                cardClass = "blue";
                backgroundResourceKey = "CourseBlueBackgroundBrush";
                break;
            case EScheduleCardKind.Personal:
                cardClass = "personal";
                backgroundResourceKey = "PersonalScheduleBackgroundBrush";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cardKind), cardKind, "Unknown schedule card kind.");
        }

        Button scheduleCard = boardGrid.Children
            .OfType<Button>()
            .Single(candidate => candidate.Classes.Contains(cardClass));
        Border scheduleCardSurface = scheduleCard.GetVisualDescendants()
            .OfType<Border>()
            .Single(candidate => candidate.Name == "PART_ScheduleCardSurface");
        Border interactionOverlay = scheduleCard.GetVisualDescendants()
            .OfType<Border>()
            .Single(candidate => candidate.Name == "PART_InteractionOverlay");
        ContentPresenter contentPresenter = scheduleCard.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(candidate => candidate.Name == "PART_ContentPresenter");
        Color expectedBackground = findRequiredThemeColor(backgroundResourceKey, themeVariant);
        Color expectedOverlayColor = findRequiredThemeColor("TextPrimaryBrush", themeVariant);

        Assert.Equal(expectedBackground, getRequiredSolidColor(scheduleCard.Background));
        Assert.Equal(expectedBackground, getRequiredSolidColor(scheduleCardSurface.Background));
        Assert.Null(contentPresenter.Background);
        Assert.Equal(expectedOverlayColor, getRequiredSolidColor(interactionOverlay.Background));
        Assert.Equal(0.0, interactionOverlay.Opacity);

        Point? cardOriginOrNull = scheduleCard.TranslatePoint(new Point(0.0, 0.0), window);
        Assert.NotNull(cardOriginOrNull);
        if (cardOriginOrNull == null)
        {
            throw new InvalidOperationException("The schedule card position could not be resolved.");
        }

        Point cardCenter = cardOriginOrNull.Value
            + new Vector(
                scheduleCard.Bounds.Width / 2.0,
                scheduleCard.Bounds.Height / 2.0);
        window.MouseMove(cardCenter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(scheduleCard.IsPointerOver);
        Assert.Equal(expectedBackground, getRequiredSolidColor(scheduleCard.Background));
        Assert.Equal(expectedBackground, getRequiredSolidColor(scheduleCardSurface.Background));
        Assert.Equal(0.04, interactionOverlay.Opacity);

        window.MouseDown(cardCenter, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(expectedBackground, getRequiredSolidColor(scheduleCard.Background));
        Assert.Equal(expectedBackground, getRequiredSolidColor(scheduleCardSurface.Background));
        Assert.Equal(0.08, interactionOverlay.Opacity);
        ITransform? pressedTransformOrNull = scheduleCard.RenderTransform;
        Assert.NotNull(pressedTransformOrNull);
        if (pressedTransformOrNull == null)
        {
            throw new InvalidOperationException("The pressed schedule card transform was not applied.");
        }

        Assert.Equal(0.99, pressedTransformOrNull.Value.M11, 3);
        Assert.Equal(0.99, pressedTransformOrNull.Value.M22, 3);

        Point outsideCard = new Point(2.0, 2.0);
        window.MouseMove(outsideCard, RawInputModifiers.None);
        window.MouseUp(outsideCard, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static Color findRequiredThemeColor(string resourceKey, ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(resourceKey, themeVariant, out resourceOrNull);
        Assert.True(hasResource, "Missing theme brush: " + resourceKey);
        return getRequiredSolidColor(resourceOrNull as IBrush);
    }

    private static void assertBitmapContainsOpaqueContent(Bitmap bitmap)
    {
        using (WriteableBitmap pixelCopy = new WriteableBitmap(
            bitmap.PixelSize,
            new Vector(96.0, 96.0),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul))
        using (ILockedFramebuffer framebuffer = pixelCopy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            int sampleX = Math.Min(10, bitmap.PixelSize.Width - 1);
            int sampleY = Math.Min(10, bitmap.PixelSize.Height - 1);
            int alphaOffset = (sampleY * framebuffer.RowBytes)
                + (sampleX * 4)
                + 3;
            byte alpha = Marshal.ReadByte(framebuffer.Address, alphaOffset);
            Assert.Equal(byte.MaxValue, alpha);
        }
    }

    private static Color getRequiredSolidColor(IBrush? brushOrNull)
    {
        SolidColorBrush? solidBrushOrNull = brushOrNull as SolidColorBrush;
        Assert.NotNull(solidBrushOrNull);
        if (solidBrushOrNull == null)
        {
            throw new InvalidOperationException("The tested background was not a solid color brush.");
        }

        return solidBrushOrNull.Color;
    }
}
