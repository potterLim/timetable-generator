using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleBoardCandidateLayoutTests
{
    [Fact]
    public void CandidateEntriesControlTimeAxisWhileSharedEntriesControlDayRange()
    {
        ScheduleEntry candidateEntry = createEntry(EDay.Monday, new ScheduleTime(12, 0), new ScheduleTime(13, 15));
        ScheduleBoardDayRange sharedDayRange = ScheduleBoardDayRange.CreateForEntries(
            new ScheduleEntry[]
            {
                candidateEntry,
                createEntry(
                    EDay.Sunday,
                    new ScheduleTime(8, 0),
                    new ScheduleTime(9, 0)),
            });

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForEntries(new ScheduleEntry[] { candidateEntry }, sharedDayRange);

        Assert.Equal(new ScheduleBoardTimeBoundary(690), layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(810), layout.TimeAxis.End);
        Assert.Equal(7, layout.DayRange.DayCount);
        Assert.Equal(EDay.Sunday, layout.DayRange.Days[^1].Day);
    }

    [AvaloniaFact]
    public void ChangingCandidatePresentationResetsVerticalScrollPosition()
    {
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createPresentation(createCourseEntry(EDay.Monday, new AcademicPeriod(1)), createCourseEntry(EDay.Monday, new AcademicPeriod(9)));
        Window window = new Window
        {
            Width = 800.0,
            Height = 320.0,
            Content = scheduleBoard,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ScrollViewer? scrollViewerOrNull = scheduleBoard.FindControl<ScrollViewer>("ScheduleScrollViewer");
            Assert.NotNull(scrollViewerOrNull);
            if (scrollViewerOrNull == null)
            {
                throw new InvalidOperationException("The schedule scroll viewer was not found.");
            }

            scrollViewerOrNull.ScrollToEnd();
            Dispatcher.UIThread.RunJobs();
            Assert.True(scrollViewerOrNull.Offset.Y > 0.0);

            scheduleBoard.DataContext = createPresentation(createCourseEntry(EDay.Tuesday, new AcademicPeriod(3)), createCourseEntry(EDay.Tuesday, new AcademicPeriod(10)));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0.0, scrollViewerOrNull.Offset.Y);
            Assert.Equal(new ScheduleBoardTimeBoundary(690), scheduleBoard.RenderedLayout.TimeAxis.Start);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MidnightStartLabelUsesTheFirstTimeRowsInsteadOfTheHeaderRow()
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(EDay.Monday, new DailyTimeRange(new ScheduleTime(0, 15), new ScheduleTime(0, 45)));
        PersonalSchedule personalSchedule = new PersonalSchedule(PersonalScheduleId.CreateNew(), new PersonalScheduleTitle("자정 일정"), new WeeklyTimeRange[] { timeRange }, PersonalScheduleDetails.CreateEmpty());
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createPresentation(new PersonalScheduleEntry(personalSchedule, timeRange));
        Window window = new Window
        {
            Width = 800.0,
            Height = 320.0,
            Content = scheduleBoard,
        };

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

            TextBlock midnightLabel = Assert.Single(
                boardGridOrNull.Children.OfType<TextBlock>(),
                textBlock => textBlock.Text == "00:00");
            Assert.Equal(1, Grid.GetRow(midnightLabel));
            Assert.Equal(2, Grid.GetRowSpan(midnightLabel));
        }
        finally
        {
            window.Close();
        }
    }

    private static ScheduleBoardPresentation createPresentation(params ScheduleEntry[] entries)
    {
        return new ScheduleBoardPresentation(new ScheduleRecommendation(entries), new PlanName("후보별 시간축 테스트"), new InstitutionName("한동대학교"), AcademicTerm.Parse("2026-2"));
    }

    private static ScheduleEntry createCourseEntry(EDay day, AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            new CourseId("course-candidate-axis"),
            new OfferingId("offering-candidate-axis-01"),
            new ScheduleCourseDetails(
                new CourseCode("AXI00100"),
                new KoreanCourseName("후보별 시간축"),
                new CourseCredits(3m),
                new ScheduleInstructorSummary(
                    InstructorAssignmentMetadata.NotProvided),
                new ScheduleLocationSummary(
                    LocationAssignmentMetadata.NotProvided)),
            new CourseSectionCode("01"),
            new MeetingSlot(day, period),
            ECourseAccent.Blue);
    }

    private static ScheduleEntry createEntry(EDay day, ScheduleTime start, ScheduleTime end)
    {
        return new TestScheduleEntry(day, new DailyTimeRange(start, end));
    }

    private sealed class TestScheduleEntry : ScheduleEntry
    {
        public TestScheduleEntry(EDay day, DailyTimeRange timeRange)
            : base(day, timeRange)
        {
        }
    }
}
