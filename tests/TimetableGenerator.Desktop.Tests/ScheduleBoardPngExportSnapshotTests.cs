using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
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
using ApplicationScheduleRecommendation =
    TimetableGenerator.Application.Scheduling.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleBoardPngExportSnapshotTests
{
    private const double EXPECTED_WEEKEND_EXPORT_SURFACE_MINIMUM_WIDTH = 996.0;

    [Fact]
    public void PngExportLayoutDefaultsToFourPm()
    {
        ScheduleEntry entry = createScheduleEntry(
            EDay.Monday,
            new AcademicPeriod(1));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { entry });

        Assert.Equal(
            new ScheduleBoardTimeBoundary(510),
            layout.TimeAxis.Start);
        Assert.Equal(
            new ScheduleBoardTimeBoundary(960),
            layout.TimeAxis.End);
        Assert.Equal(90, layout.TimeAxis.IncrementCount);
        Assert.Equal("15:30", layout.TimeAxis.LabelTimes[^1].ToString());
        Assert.DoesNotContain(
            layout.TimeAxis.LabelTimes,
            boundary => boundary.ToString() == "16:00");
    }

    [Fact]
    public void PngExportLayoutExtendsSixthPeriodToNextHalfHourBoundary()
    {
        ScheduleEntry entry = createScheduleEntry(
            EDay.Thursday,
            new AcademicPeriod(6));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { entry });

        Assert.Equal(
            new ScheduleBoardTimeBoundary(1_050),
            layout.TimeAxis.End);
        Assert.Equal("17:00", layout.TimeAxis.LabelTimes[^1].ToString());
        Assert.Equal(
            3,
            layout.TimeAxis.IncrementCount
                - layout.TimeAxis.FindEndingRowOffset(entry.TimeRange.End));
    }

    [Fact]
    public void PngExportLayoutIncludesSaturdayWhenSundayIsTheOnlyWeekendEntry()
    {
        ScheduleEntry sundayEntry = createScheduleEntry(
            EDay.Sunday,
            new AcademicPeriod(2));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForPngExport(
            new ScheduleEntry[] { sundayEntry });

        Assert.Equal(7, layout.DayRange.DayCount);
        Assert.Equal(EDay.Saturday, layout.DayRange.Days[5].Day);
        Assert.Equal(EDay.Sunday, layout.DayRange.Days[6].Day);
        Assert.Equal(6, layout.DayRange.FindDay(EDay.Saturday).ColumnIndex);
        Assert.Equal(7, layout.DayRange.FindDay(EDay.Sunday).ColumnIndex);
    }

    [AvaloniaFact]
    public async Task NarrowSundayBoardExportsEveryWeekendColumnAtReadableWidthAsync()
    {
        ScheduleEntry sundayEntry = createScheduleEntry(
            EDay.Sunday,
            new AcademicPeriod(2));
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
                Assert.True(
                    snapshot.Surface.Bounds.Width
                        >= EXPECTED_WEEKEND_EXPORT_SURFACE_MINIMUM_WIDTH);

                Grid boardGrid = findBoardGrid(snapshot.Surface);
                Assert.Equal(8, boardGrid.ColumnDefinitions.Count);
                assertWeekendHeadersArePresent(boardGrid);
                assertDayColumnsMeetMinimumWidth(boardGrid, 132.0);

                Button exportCard = Assert.Single(
                    boardGrid.Children.OfType<Button>());
                Grid exportCardContent = Assert.IsType<Grid>(exportCard.Content);
                List<TextBlock> exportCardTexts = exportCardContent.Children
                    .OfType<TextBlock>()
                    .ToList();
                Assert.Equal(
                    new string[]
                    {
                        "시간표 내보내기 검증",
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

                AvaloniaControlPngExporter exporter =
                    new AvaloniaControlPngExporter(PngExportScale.Create(1.0));
                using (MemoryStream destinationStream = new MemoryStream())
                {
                    await exporter.ExportControlAsync(
                        snapshot.Surface,
                        destinationStream,
                        CancellationToken.None);
                    destinationStream.Position = 0L;
                    using (Bitmap bitmap = new Bitmap(destinationStream))
                    {
                        Assert.True(
                            bitmap.PixelSize.Width
                                >= EXPECTED_WEEKEND_EXPORT_SURFACE_MINIMUM_WIDTH);
                        Assert.Equal(
                            (int)Math.Ceiling(snapshot.Surface.Bounds.Height),
                            bitmap.PixelSize.Height);
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
        ScheduleBoardView sourceBoard = createSourceBoard(
            projectedRecommendation.Entries);
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
                Button exportCard = Assert.Single(
                    boardGrid.Children.OfType<Button>());
                Grid exportCardContent = Assert.IsType<Grid>(exportCard.Content);
                List<TextBlock> exportCardTexts = exportCardContent.Children
                    .OfType<TextBlock>()
                    .ToList();
                Assert.Equal(
                    new string[] { "프로그래밍 I" },
                    exportCardTexts.Select(textBlock => textBlock.Text));
                Assert.DoesNotContain(
                    exportCardTexts,
                    textBlock => textBlock.Text == "교수 정보 없음");
                Assert.DoesNotContain(
                    exportCardTexts,
                    textBlock => textBlock.Text == "강의실 미정");

                AvaloniaControlPngExporter exporter =
                    new AvaloniaControlPngExporter(PngExportScale.Create(1.0));
                using (MemoryStream destinationStream = new MemoryStream())
                {
                    await exporter.ExportControlAsync(
                        snapshot.Surface,
                        destinationStream,
                        CancellationToken.None);
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
    public void PointerOverCourseCardKeepsItsThemedBackgroundInBothModes()
    {
        ScheduleEntry entry = createScheduleEntry(
            EDay.Monday,
            new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = createSourceBoard(
            new ScheduleEntry[] { entry });
        Window window = createWindow(scheduleBoard, ThemeVariant.Light);

        try
        {
            window.Show();
            assertPointerOverCardBackground(
                window,
                scheduleBoard,
                ThemeVariant.Light);
            assertPointerOverCardBackground(
                window,
                scheduleBoard,
                ThemeVariant.Dark);
        }
        finally
        {
            window.Close();
        }
    }

    private static ScheduleBoardView createSourceBoard(
        IReadOnlyList<ScheduleEntry> entries)
    {
        ScheduleBoardView sourceBoard = new ScheduleBoardView();
        sourceBoard.DataContext = new ScheduleBoardPresentation(
            new ScheduleRecommendation(entries),
            new PlanName("PNG 내보내기 테스트"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"));
        return sourceBoard;
    }

    private static Window createWindow(
        Control content,
        ThemeVariant themeVariant)
    {
        Window window = new Window();
        window.Width = 900.0;
        window.Height = 620.0;
        window.RequestedThemeVariant = themeVariant;
        window.Content = content;
        return window;
    }

    private static ScheduleEntry createScheduleEntry(
        EDay day,
        AcademicPeriod period)
    {
        return new CourseScheduleEntry(
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
            day,
            period,
            ECourseAccent.Blue);
    }

    private static Grid findBoardGrid(Control surface)
    {
        Grid? boardGridOrNull = surface.GetVisualDescendants()
            .OfType<Grid>()
            .SingleOrDefault(candidate => candidate.Name == "BoardGrid");
        Assert.NotNull(boardGridOrNull);
        if (boardGridOrNull == null)
        {
            throw new InvalidOperationException(
                "The PNG snapshot schedule grid was not rendered.");
        }

        return boardGridOrNull;
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

    private static void assertDayColumnsMeetMinimumWidth(
        Grid boardGrid,
        double minimumWidth)
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

    private static void assertPointerOverCardBackground(
        Window window,
        ScheduleBoardView scheduleBoard,
        ThemeVariant themeVariant)
    {
        window.RequestedThemeVariant = themeVariant;
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        Grid boardGrid = findBoardGrid(scheduleBoard.PngExportSurface);
        Button scheduleCard = Assert.Single(
            boardGrid.Children.OfType<Button>());
        ContentPresenter contentPresenter = scheduleCard.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(candidate => candidate.Name == "PART_ContentPresenter");
        Color expectedBackground = findRequiredThemeColor(
            "CourseBlueBackgroundBrush",
            themeVariant);

        Point? cardOriginOrNull = scheduleCard.TranslatePoint(
            new Point(0.0, 0.0),
            window);
        Assert.NotNull(cardOriginOrNull);
        if (cardOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The schedule card position could not be resolved.");
        }

        Point cardCenter = cardOriginOrNull.Value
            + new Vector(
                scheduleCard.Bounds.Width / 2.0,
                scheduleCard.Bounds.Height / 2.0);
        window.MouseMove(cardCenter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(scheduleCard.IsPointerOver);
        Assert.Equal(
            expectedBackground,
            getRequiredSolidColor(scheduleCard.Background));
        Assert.Equal(
            expectedBackground,
            getRequiredSolidColor(contentPresenter.Background));
    }

    private static Color findRequiredThemeColor(
        string resourceKey,
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
            resourceKey,
            themeVariant,
            out resourceOrNull);
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
            throw new InvalidOperationException(
                "The tested background was not a solid color brush.");
        }

        return solidBrushOrNull.Color;
    }
}
