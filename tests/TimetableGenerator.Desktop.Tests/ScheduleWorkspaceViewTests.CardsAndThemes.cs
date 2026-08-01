using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

using Xunit;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceViewTests
{
    [AvaloniaFact]
    public void CourseScheduleCardPrioritizesTitleLocationThenInstructorAndKeepsDetails()
    {
        const string LONG_NAME = "사용자 경험과 인터페이스 설계를 위한 고급 프로젝트 실습";
        const string LONG_INSTRUCTOR = "김테스트, 박테스트 외 3명";
        const string LONG_LOCATION = "느헤미야홀 401호 공동 프로젝트 스튜디오";

        ScheduleEntry entry = createLongScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }));

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
            Assert.Contains("01분반", AutomationProperties.GetName(scheduleCard));
            Assert.Contains("3학점", AutomationProperties.GetName(scheduleCard));
            Assert.Equal("선택하면 과목의 전체 시간, 교수, 강의실 정보를 엽니다.", AutomationProperties.GetHelpText(scheduleCard));

            Grid cardContent = Assert.IsType<Grid>(scheduleCard.Content);
            Assert.Equal(VerticalAlignment.Center, cardContent.VerticalAlignment);
            Assert.Equal(3, cardContent.RowDefinitions.Count);
            Assert.All(
                cardContent.RowDefinitions,
                rowDefinition => Assert.True(rowDefinition.Height.IsAuto));

            List<TextBlock> cardTexts = scheduleCard.GetVisualDescendants().OfType<TextBlock>().ToList();
            Assert.Equal(3, cardTexts.Count);
            Assert.DoesNotContain(
                cardTexts,
                textBlock => textBlock.Text == "UXD00100");
            Assert.DoesNotContain(
                cardTexts,
                textBlock => textBlock.Text == "3학점");
            TextBlock courseName = Assert.Single(
                cardTexts,
                textBlock => textBlock.Text == LONG_NAME + "(01)");
            Assert.Equal(0, Grid.GetRow(courseName));
            Assert.Equal(TextAlignment.Center, courseName.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, courseName.TextWrapping);
            Assert.Equal(14.0, courseName.FontSize);
            Assert.Equal(18.0, courseName.LineHeight);
            Assert.Equal(2, courseName.MaxLines);
            Assert.Equal(TextTrimming.CharacterEllipsis, courseName.TextTrimming);
            TextBlock instructor = Assert.Single(
                cardTexts,
                textBlock => textBlock.Text == LONG_INSTRUCTOR);
            TextBlock location = Assert.Single(
                cardTexts,
                textBlock => textBlock.Text == LONG_LOCATION);
            Assert.Equal(1, Grid.GetRow(location));
            Assert.Equal(2, Grid.GetRow(instructor));
            Assert.Equal(7.0, location.Margin.Top);
            Assert.Equal(2.0, instructor.Margin.Top);
            Assert.Equal(TextAlignment.Center, location.TextAlignment);
            Assert.Equal(TextAlignment.Center, instructor.TextAlignment);
            Assert.Equal(14.0, location.LineHeight);
            Assert.Equal(12.0, instructor.LineHeight);
            Assert.True(courseName.FontSize > location.FontSize);
            Assert.True(location.FontSize > instructor.FontSize);
            Assert.Equal(FontWeight.Bold, courseName.FontWeight);
            Assert.Equal(FontWeight.SemiBold, location.FontWeight);
            Assert.Equal(FontWeight.Normal, instructor.FontWeight);
            Assert.NotSame(location.Foreground, instructor.Foreground);
            Assert.Equal(TextTrimming.CharacterEllipsis, instructor.TextTrimming);
            Assert.Equal(TextTrimming.CharacterEllipsis, location.TextTrimming);
            Assert.Equal(LONG_NAME + " · 01분반" + Environment.NewLine + "선택하여 과목 상세 정보 보기", ToolTip.GetTip(scheduleCard));
            Assert.Equal(15, Grid.GetRowSpan(scheduleCard));

            FlyoutBase? detailsFlyoutOrNull = scheduleCard.Flyout;
            Assert.NotNull(detailsFlyoutOrNull);
            if (detailsFlyoutOrNull == null)
            {
                throw new InvalidOperationException("The schedule detail flyout was not found.");
            }

            bool hasFocusedCard = scheduleCard.Focus(NavigationMethod.Tab);
            Assert.True(hasFocusedCard);
            Dispatcher.UIThread.RunJobs();
            Border focusedCardSurface = scheduleCard.GetVisualDescendants()
                .OfType<Border>()
                .Single(
                    candidate => candidate.Name
                        == "PART_ScheduleCardSurface");
            Assert.Equal(new Thickness(2.0), focusedCardSurface.BorderThickness);
            SolidColorBrush expectedFocusBrush = findRequiredThemeBrush(FOCUS, scheduleCard.ActualThemeVariant);
            SolidColorBrush actualFocusBrush = Assert.IsType<SolidColorBrush>(focusedCardSurface.BorderBrush);
            Assert.Equal(expectedFocusBrush.Color, actualFocusBrush.Color);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
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
    public void CourseScheduleCardOmitsUnavailableMetadataWithoutEmptyRows()
    {
        const string LOCATION_ONLY_NAME = "장소만 제공된 과목";
        const string INSTRUCTOR_ONLY_NAME = "교수만 제공된 과목";
        const string TITLE_ONLY_NAME = "세부 정보가 없는 과목";

        ScheduleEntry locationOnlyEntry = createMetadataAvailabilityEntry(
            new CourseCode("LOC00100"),
            new KoreanCourseName(LOCATION_ONLY_NAME),
            EDay.Monday,
            new ScheduleInstructorSummary(
                InstructorAssignmentMetadata.NotProvided),
            new ScheduleLocationSummary(
                LocationAssignmentMetadata.CreateAssigned(new ClassroomDisplayText("오석관 301"))));
        ScheduleEntry instructorOnlyEntry = createMetadataAvailabilityEntry(
            new CourseCode("INS00100"),
            new KoreanCourseName(INSTRUCTOR_ONLY_NAME),
            EDay.Tuesday,
            new ScheduleInstructorSummary(
                InstructorAssignmentMetadata.CreateConfirmed(
                    new InstructorDisplayText("김테스트"),
                    new AdditionalInstructorCount(0))),
            new ScheduleLocationSummary(LocationAssignmentMetadata.NotProvided));
        ScheduleEntry titleOnlyEntry = createMetadataAvailabilityEntry(
            new CourseCode("NON00100"),
            new KoreanCourseName(TITLE_ONLY_NAME),
            EDay.Wednesday,
            new ScheduleInstructorSummary(
                InstructorAssignmentMetadata.Unconfirmed),
            new ScheduleLocationSummary(LocationAssignmentMetadata.NotProvided));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    locationOnlyEntry,
                    instructorOnlyEntry,
                    titleOnlyEntry,
                }));

        Window window = new Window();
        window.Width = 900.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button locationOnlyCard = findCourseCardByName(scheduleBoard, LOCATION_ONLY_NAME);
            assertVisualCourseCardTexts(locationOnlyCard, new string[] { LOCATION_ONLY_NAME + "(01)", "오석관 301" });
            Grid locationOnlyContent = Assert.IsType<Grid>(locationOnlyCard.Content);
            TextBlock locationOnlyLocation = Assert.IsType<TextBlock>(locationOnlyContent.Children[1]);
            Assert.Equal(7.0, locationOnlyLocation.Margin.Top);
            Assert.Equal(14.0, locationOnlyLocation.LineHeight);

            Button instructorOnlyCard = findCourseCardByName(scheduleBoard, INSTRUCTOR_ONLY_NAME);
            assertVisualCourseCardTexts(instructorOnlyCard, new string[] { INSTRUCTOR_ONLY_NAME + "(01)", "김테스트" });
            Grid instructorOnlyContent = Assert.IsType<Grid>(instructorOnlyCard.Content);
            TextBlock instructorOnlyInstructor = Assert.IsType<TextBlock>(instructorOnlyContent.Children[1]);
            Assert.Equal(7.0, instructorOnlyInstructor.Margin.Top);
            Assert.Equal(12.0, instructorOnlyInstructor.LineHeight);

            Button titleOnlyCard = findCourseCardByName(scheduleBoard, TITLE_ONLY_NAME);
            assertVisualCourseCardTexts(titleOnlyCard, new string[] { TITLE_ONLY_NAME + "(01)" });
            Assert.Contains("교수 미정", AutomationProperties.GetName(titleOnlyCard));
            Assert.Contains("강의실 미정", AutomationProperties.GetName(titleOnlyCard));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProjectedUnavailableMetadataIsOmittedFromScheduleCard()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(document);
        ApplicationScheduleRecommendation recommendation = CatalogProjectionTestFixture.CreateScheduledRecommendation(document, new CourseId("course-programming"), new OfferingId("offering-programming-alternative"));
        ScheduleRecommendation projectedRecommendation = ScheduleRecommendationProjector.Project(recommendation, catalogProjection);
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(projectedRecommendation);
        Window window = new Window();
        window.Width = 900.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button projectedCard = findCourseCardByName(scheduleBoard, "프로그래밍 I");
            assertVisualCourseCardTexts(projectedCard, new string[] { "프로그래밍 I(02)" });
            Assert.Contains("교수 정보 없음", AutomationProperties.GetName(projectedCard));
            Assert.Contains("강의실 미정", AutomationProperties.GetName(projectedCard));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleBoardRebuildsCodeGeneratedBrushesWhenThemeChanges()
    {
        ScheduleEntry entry = createScheduleEntry(EDay.Monday, new AcademicPeriod(1));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }));

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.RequestedThemeVariant = ThemeVariant.Light;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            RenderedScheduleBrushes lightBrushes = findRenderedScheduleBrushes(scheduleBoard);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();

            RenderedScheduleBrushes darkBrushes = findRenderedScheduleBrushes(scheduleBoard);
            SolidColorBrush expectedDarkBorder = findRequiredThemeBrush(BORDER, ThemeVariant.Dark);
            SolidColorBrush expectedDarkSecondary = findRequiredThemeBrush(TEXT_SECONDARY, ThemeVariant.Dark);
            SolidColorBrush expectedDarkAccent = findRequiredThemeBrush(ACCENT, ThemeVariant.Dark);

            Assert.NotSame(lightBrushes.ScheduleCard, darkBrushes.ScheduleCard);
            Assert.NotEqual(lightBrushes.CellBorder.Color, darkBrushes.CellBorder.Color);
            Assert.NotEqual(lightBrushes.TimeLabel.Color, darkBrushes.TimeLabel.Color);
            Assert.NotEqual(lightBrushes.DetailAccent.Color, darkBrushes.DetailAccent.Color);
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
    public void ScheduleBoardUsesAContinuousOuterFrameInProductThemes()
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

            Border? boardFrameOrNull = scheduleBoard.FindControl<Border>("BoardFrame");
            Assert.NotNull(boardFrameOrNull);
            if (boardFrameOrNull == null)
            {
                throw new InvalidOperationException("The timetable outer frame was not available.");
            }

            Assert.True(boardFrameOrNull.UseLayoutRounding);
            Assert.Equal(new Thickness(1.0), boardFrameOrNull.BorderThickness);
            Assert.Equal(new CornerRadius(7.0), boardFrameOrNull.CornerRadius);
            Assert.Null(scheduleBoard.FindControl<Border>("BoardContentRightBoundary"));
            Border pngExportCanvas = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("PngExportCanvas"));
            Border boardExportSurface = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("BoardExportSurface"));
            Grid boardGrid = Assert.IsType<Grid>(scheduleBoard.FindControl<Grid>("BoardGrid"));
            Assert.Equal(new Thickness(0.0), pngExportCanvas.Padding);
            Assert.Same(boardExportSurface, scheduleBoard.PngExportSurface);
            Assert.DoesNotContain(
                boardGrid.Children.OfType<Border>(),
                border => border.Classes.Contains("schedule-end-boundary"));

            ThemeVariant[] themeVariants = new ThemeVariant[]
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };
            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();

                SolidColorBrush expectedBrush = findRequiredThemeBrush(STRONG_BORDER, themeVariant);
                SolidColorBrush frameBrush = Assert.IsType<SolidColorBrush>(boardFrameOrNull.BorderBrush);
                Assert.Equal(expectedBrush.Color, frameBrush.Color);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
