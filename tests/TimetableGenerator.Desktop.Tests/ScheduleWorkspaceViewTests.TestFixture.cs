using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceViewTests
{
    private static readonly ColorToken BORDER = new ColorToken("BorderBrush");
    private static readonly ColorToken STRONG_BORDER = new ColorToken("StrongBorderBrush");
    private static readonly ColorToken TEXT_SECONDARY = new ColorToken("TextSecondaryBrush");
    private static readonly ColorToken ACCENT = new ColorToken("AccentBrush");
    private static readonly ColorToken FOCUS = new ColorToken("ProductFocusStrokeBrush");

    private static ScheduleBoardPresentation createScheduleBoardPresentation(ScheduleRecommendation schedule)
    {
        return new ScheduleBoardPresentation(schedule, new PlanName("테스트 계획"), new InstitutionName("한동대학교"), AcademicTerm.Parse("2026-2"));
    }

    private static ScheduleEntry createScheduleEntry(EDay day, AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            new CourseId("course-tst00100"),
            new OfferingId("offering-tst00100-01"),
            new ScheduleCourseDetails(
                new CourseCode("TST00100"),
                new KoreanCourseName("저녁 수업"),
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

    private static ScheduleEntry createLongScheduleEntry(EDay day, AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            new CourseId("course-uxd00100"),
            new OfferingId("offering-uxd00100-01"),
            new ScheduleCourseDetails(
                new CourseCode("UXD00100"),
                new KoreanCourseName(
                    "사용자 경험과 인터페이스 설계를 위한 고급 프로젝트 실습"),
                new CourseCredits(3m),
                new ScheduleInstructorSummary(
                    InstructorAssignmentMetadata.CreateConfirmed(
                        new InstructorDisplayText("김테스트, 박테스트 외 3명"),
                        new AdditionalInstructorCount(3))),
                new ScheduleLocationSummary(
                    LocationAssignmentMetadata.CreateAssigned(
                        new ClassroomDisplayText(
                            "느헤미야홀 401호 공동 프로젝트 스튜디오")))),
            new CourseSectionCode("01"),
            new MeetingSlot(day, period),
            ECourseAccent.Blue);
    }

    private static ScheduleEntry createMetadataAvailabilityEntry(
        CourseCode code,
        KoreanCourseName name,
        EDay day,
        ScheduleInstructorSummary instructorSummary,
        ScheduleLocationSummary locationSummary)
    {
        return new CourseScheduleEntry(
            new CourseId("course-" + code.Value),
            new OfferingId("offering-" + code.Value + "-01"),
            new ScheduleCourseDetails(
                code,
                name,
                new CourseCredits(3m),
                instructorSummary,
                locationSummary),
            new CourseSectionCode("01"),
            new MeetingSlot(day, new AcademicPeriod(1)),
            ECourseAccent.Blue);
    }

    private static Button findCourseCardByName(ScheduleBoardView scheduleBoard, string courseName)
    {
        Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
        Assert.NotNull(boardGridOrNull);
        if (boardGridOrNull == null)
        {
            throw new InvalidOperationException("The rendered schedule grid was not found.");
        }

        Button? matchingCardOrNull = null;
        foreach (Button scheduleCard in boardGridOrNull.Children.OfType<Button>())
        {
            string? accessibleNameOrNull = AutomationProperties.GetName(scheduleCard);
            if (accessibleNameOrNull != null && accessibleNameOrNull.Contains(courseName, StringComparison.Ordinal))
            {
                matchingCardOrNull = scheduleCard;
                break;
            }
        }

        Assert.NotNull(matchingCardOrNull);
        if (matchingCardOrNull == null)
        {
            throw new InvalidOperationException("The requested course schedule card was not found.");
        }

        return matchingCardOrNull;
    }

    private static void assertVisualCourseCardTexts(Button scheduleCard, IReadOnlyList<string> expectedTexts)
    {
        Grid cardContent = Assert.IsType<Grid>(scheduleCard.Content);
        Assert.Equal(VerticalAlignment.Center, cardContent.VerticalAlignment);
        Assert.Equal(expectedTexts.Count, cardContent.RowDefinitions.Count);
        Assert.Equal(expectedTexts.Count, cardContent.Children.Count);

        for (int index = 0; index < expectedTexts.Count; ++index)
        {
            TextBlock textBlock = Assert.IsType<TextBlock>(cardContent.Children[index]);
            Assert.Equal(expectedTexts[index], textBlock.Text);
            Assert.Equal(index, Grid.GetRow(textBlock));
            Assert.Equal(TextAlignment.Center, textBlock.TextAlignment);
        }

        IReadOnlyList<string> unavailableTexts = new string[]
        {
            "교수 정보 없음",
            "교수 미정",
            "강의실 미정",
        };
        foreach (string unavailableText in unavailableTexts)
        {
            Assert.DoesNotContain(
                cardContent.Children.OfType<TextBlock>(),
                textBlock => textBlock.Text == unavailableText);
        }
    }

    private static RenderedScheduleBrushes findRenderedScheduleBrushes(ScheduleBoardView scheduleBoard)
    {
        Grid? boardGridOrNull = scheduleBoard.FindControl<Grid>("BoardGrid");
        Assert.NotNull(boardGridOrNull);
        if (boardGridOrNull == null)
        {
            throw new InvalidOperationException("The rendered schedule grid was not found.");
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
                && timeText.Text.Contains("09:00", StringComparison.Ordinal))
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
        if (cellOrNull == null || timeLabelOrNull == null || scheduleCardOrNull == null)
        {
            throw new InvalidOperationException("The generated schedule visuals were incomplete.");
        }

        SolidColorBrush cellBorder = Assert.IsType<SolidColorBrush>(cellOrNull.BorderBrush);
        SolidColorBrush timeLabel = Assert.IsType<SolidColorBrush>(timeLabelOrNull.Foreground);
        Flyout detailsFlyout = Assert.IsType<Flyout>(scheduleCardOrNull.Flyout);
        Border detailsSurface = Assert.IsType<Border>(detailsFlyout.Content);
        StackPanel details = Assert.IsType<StackPanel>(detailsSurface.Child);
        TextBlock identity = Assert.IsType<TextBlock>(details.Children[0]);
        SolidColorBrush detailAccent = Assert.IsType<SolidColorBrush>(identity.Foreground);

        return new RenderedScheduleBrushes(scheduleCardOrNull, cellBorder, timeLabel, detailAccent);
    }

    private static void assertBoardUsesAutomaticVerticalScrolling(ScheduleBoardView scheduleBoard, Grid boardGrid, ScrollViewer scrollViewer)
    {
        Border? exportSurfaceOrNull = scheduleBoard.FindControl<Border>("BoardExportSurface");
        ScrollBar? verticalScrollBarOrNull = scrollViewer.GetVisualDescendants()
            .OfType<ScrollBar>()
            .SingleOrDefault(scrollBar => scrollBar.Orientation == Orientation.Vertical);
        Assert.NotNull(exportSurfaceOrNull);
        Assert.NotNull(verticalScrollBarOrNull);
        if (exportSurfaceOrNull == null || verticalScrollBarOrNull == null)
        {
            throw new InvalidOperationException("The timetable scrollbar geometry was not available.");
        }

        Border exportSurface = exportSurfaceOrNull;
        ScrollBar verticalScrollBar = verticalScrollBarOrNull;
        Assert.True(scrollViewer.AllowAutoHide);
        Assert.True(verticalScrollBar.IsEffectivelyVisible);
        Assert.Equal(new Thickness(0.0, 6.0, 4.0, 6.0), verticalScrollBar.Margin);
        Assert.Equal(new Thickness(0.0), exportSurface.BorderThickness);
        Assert.Equal(scrollViewer.Viewport.Width, exportSurface.Bounds.Width, 3);
        Assert.Equal(exportSurface.Bounds.Width, boardGrid.Bounds.Width, 3);
        Assert.Null(scheduleBoard.FindControl<Border>("BoardContentRightBoundary"));
    }

    private static void assertStickyHeaderMatchesBoardSurface(ScheduleBoardView scheduleBoard)
    {
        Border? stickyHeaderContainerOrNull = scheduleBoard.FindControl<Border>("BoardStickyHeaderContainer");
        Border? stickyHeaderSurfaceOrNull = scheduleBoard.FindControl<Border>("BoardStickyDayHeaderSurface");
        Border? exportSurfaceOrNull = scheduleBoard.FindControl<Border>("BoardExportSurface");
        Assert.NotNull(stickyHeaderContainerOrNull);
        Assert.NotNull(stickyHeaderSurfaceOrNull);
        Assert.NotNull(exportSurfaceOrNull);
        if (stickyHeaderContainerOrNull == null
            || stickyHeaderSurfaceOrNull == null
            || exportSurfaceOrNull == null)
        {
            throw new InvalidOperationException("The timetable header and board surfaces were not available.");
        }

        Assert.Equal(new Thickness(0.0), stickyHeaderContainerOrNull.BorderThickness);
        Assert.Equal(new Thickness(0.0, 0.0, 0.0, 1.0), stickyHeaderSurfaceOrNull.BorderThickness);
        Assert.Equal(new Thickness(0.0), exportSurfaceOrNull.BorderThickness);
        Assert.Equal(exportSurfaceOrNull.Bounds.Width, stickyHeaderSurfaceOrNull.Bounds.Width, 3);
        Assert.Equal(stickyHeaderContainerOrNull.Bounds.Width, stickyHeaderSurfaceOrNull.Bounds.Width, 3);

        Point? headerOriginOrNull = stickyHeaderSurfaceOrNull.TranslatePoint(new Point(0.0, 0.0), scheduleBoard);
        Point? exportOriginOrNull = exportSurfaceOrNull.TranslatePoint(new Point(0.0, 0.0), scheduleBoard);
        Assert.NotNull(headerOriginOrNull);
        Assert.NotNull(exportOriginOrNull);
        if (headerOriginOrNull == null || exportOriginOrNull == null)
        {
            throw new InvalidOperationException("The timetable header and board positions were not available.");
        }

        double headerRight = headerOriginOrNull.Value.X + stickyHeaderSurfaceOrNull.Bounds.Width;
        double exportRight = exportOriginOrNull.Value.X + exportSurfaceOrNull.Bounds.Width;
        Assert.Equal(exportRight, headerRight, 3);
    }

    private static void assertDayColumnsAreEqual(Grid boardGrid)
    {
        Assert.True(boardGrid.ColumnDefinitions.Count > 1);
        double firstDayWidth = boardGrid.ColumnDefinitions[1].ActualWidth;
        for (int columnIndex = 2; columnIndex < boardGrid.ColumnDefinitions.Count; ++columnIndex)
        {
            double dayWidth = boardGrid.ColumnDefinitions[columnIndex].ActualWidth;
            Assert.InRange(Math.Abs(firstDayWidth - dayWidth), 0.0, 1.0);
        }
    }

    private static void assertScheduleUsesOuterFrameWithoutEndBoundary(Grid boardGrid)
    {
        Assert.DoesNotContain(
            boardGrid.Children.OfType<Border>(),
            border => border.Classes.Contains("schedule-end-boundary"));
    }

    private static void assertTimeLabelIsCenteredOnGuide(Grid boardGrid, TextBlock timeLabel, Border hourGuide)
    {
        Assert.Equal(48.0, timeLabel.Width);
        Assert.Equal(16.0, timeLabel.Height);
        Assert.Equal(2, Grid.GetRowSpan(timeLabel));
        Assert.Equal(TextAlignment.Right, timeLabel.TextAlignment);
        Assert.Equal(HorizontalAlignment.Right, timeLabel.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, timeLabel.VerticalAlignment);
        Assert.Equal(18.0, timeLabel.Margin.Right);

        Point? labelOriginOrNull = timeLabel.TranslatePoint(new Point(0.0, 0.0), boardGrid);
        Point? guideOriginOrNull = hourGuide.TranslatePoint(new Point(0.0, 0.0), boardGrid);
        Assert.NotNull(labelOriginOrNull);
        Assert.NotNull(guideOriginOrNull);
        if (labelOriginOrNull == null || guideOriginOrNull == null)
        {
            throw new InvalidOperationException("The time label geometry could not be resolved.");
        }

        double labelCenterY = labelOriginOrNull.Value.Y + (timeLabel.Bounds.Height / 2.0);
        double guideTopY = guideOriginOrNull.Value.Y;
        Assert.InRange(Math.Abs(labelCenterY - guideTopY), 0.0, 0.5);

        double labelRight = labelOriginOrNull.Value.X + timeLabel.Bounds.Width;
        double guideLeft = guideOriginOrNull.Value.X;
        Assert.Equal(10.0, guideLeft - labelRight, 3);
    }

    private static SolidColorBrush findRequiredThemeBrush(ColorToken colorToken, ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(colorToken.Value, themeVariant, out resourceOrNull);
        Assert.True(hasResource, "Missing brush resource: " + colorToken.Value);
        return Assert.IsType<SolidColorBrush>(resourceOrNull);
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct RenderedScheduleBrushes(Button ScheduleCard, SolidColorBrush CellBorder, SolidColorBrush TimeLabel, SolidColorBrush DetailAccent);
}
