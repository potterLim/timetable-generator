using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class PersonalScheduleInteractionTests
{
    [AvaloniaFact]
    public void PersonalScheduleBoardCardUsesExactFiveMinutePlacement()
    {
        PersonalSchedule schedule = createPersonalSchedule();
        WeeklyTimeRange timeRange = schedule.TimeRanges[0];
        PersonalScheduleEntry entry = new PersonalScheduleEntry(schedule, timeRange);
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid boardGrid = findRequiredControl<Grid>(scheduleBoard, "BoardGrid");
            Button scheduleCard = boardGrid.Children.OfType<Button>().Single();

            Assert.Equal(11, Grid.GetRow(scheduleCard));
            Assert.Equal(12, Grid.GetRowSpan(scheduleCard));
            Assert.Contains("personal", scheduleCard.Classes);
            Assert.Contains("수요일 12:20–13:20", AutomationProperties.GetName(scheduleCard));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleBoardCardMatchesCourseCardHierarchy()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(12, 0), new ScheduleTime(13, 0));
        PersonalScheduleDetails details = new PersonalScheduleDetails(new PersonalScheduleSection("A"), new PersonalScheduleInstructor("김교수"), new PersonalScheduleLocation("느헤미야홀 101호"));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("사용자 경험 연구 정기 회의"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Wednesday, timeRange),
            },
            details);
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    new PersonalScheduleEntry(schedule, schedule.TimeRanges[0]),
                }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button scheduleCard = findRequiredControl<Grid>(scheduleBoard, "BoardGrid").Children.OfType<Button>().Single();
            Grid cardContent = Assert.IsType<Grid>(scheduleCard.Content);
            TextBlock[] cardTexts = cardContent.Children.OfType<TextBlock>().ToArray();
            Assert.Equal(new string[] { "사용자 경험 연구 정기 회의(A)", "느헤미야홀 101호", "김교수" }, cardTexts.Select(getTextOrEmpty));
            Assert.Equal(new Thickness(8.0, 4.0), scheduleCard.Padding);
            Assert.Equal(VerticalAlignment.Center, cardContent.VerticalAlignment);
            Assert.Equal(3, cardContent.RowDefinitions.Count);

            TextBlock title = cardTexts[0];
            Assert.Equal(14.0, title.FontSize);
            Assert.Equal(18.0, title.LineHeight);
            Assert.Equal(FontWeight.Bold, title.FontWeight);
            Assert.Equal(2, title.MaxLines);
            Assert.Equal(TextAlignment.Center, title.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
            Assert.True(title.Bounds.Height > title.LineHeight);

            double availableContentHeight = scheduleCard.Bounds.Height - scheduleCard.Padding.Top - scheduleCard.Padding.Bottom - scheduleCard.BorderThickness.Top - scheduleCard.BorderThickness.Bottom;
            Assert.True(cardContent.DesiredSize.Height <= availableContentHeight);

            TextBlock location = cardTexts[1];
            Assert.Equal(11.5, location.FontSize);
            Assert.Equal(14.0, location.LineHeight);
            Assert.Equal(FontWeight.SemiBold, location.FontWeight);
            Assert.Equal(7.0, location.Margin.Top);
            Assert.Equal(TextAlignment.Center, location.TextAlignment);

            TextBlock responsiblePerson = cardTexts[2];
            Assert.Equal(10.5, responsiblePerson.FontSize);
            Assert.Equal(12.0, responsiblePerson.LineHeight);
            Assert.Equal(FontWeight.Normal, responsiblePerson.FontWeight);
            Assert.Equal(2.0, responsiblePerson.Margin.Top);
            Assert.Equal(TextAlignment.Center, responsiblePerson.TextAlignment);

            string? accessibleNameOrNull = AutomationProperties.GetName(scheduleCard);
            Assert.NotNull(accessibleNameOrNull);
            if (accessibleNameOrNull == null)
            {
                throw new InvalidOperationException("The personal schedule card accessible name was missing.");
            }

            string accessibleName = accessibleNameOrNull;
            Assert.Contains("분반 A", accessibleName);
            Assert.Contains("수요일 12:00–13:00", accessibleName);
            Assert.Equal("사용자 경험 연구 정기 회의(A)" + Environment.NewLine + "선택하여 개인 일정 상세 정보 보기", ToolTip.GetTip(scheduleCard));
            Assert.DoesNotContain(
                cardTexts,
                textBlock => getTextOrEmpty(textBlock).Contains(
                    "개인 일정",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                cardTexts,
                textBlock => getTextOrEmpty(textBlock).Contains(
                    "12:00",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                cardTexts,
                textBlock => getTextOrEmpty(textBlock).Contains(
                    "분반",
                    StringComparison.Ordinal));

            Button exportCard = scheduleBoard.PngExportSurface.GetVisualDescendants().OfType<Button>().Single();
            Grid exportContent = Assert.IsType<Grid>(exportCard.Content);
            TextBlock[] exportCardTexts = exportContent.Children.OfType<TextBlock>().ToArray();
            Assert.Equal(new string[] { "사용자 경험 연구 정기 회의(A)", "느헤미야홀 101호", "김교수" }, exportCardTexts.Select(getTextOrEmpty));
            Assert.Equal(2.0, exportCardTexts[2].Margin.Top);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EarlyShortScheduleShowsAClockLabelAndUsableTarget()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(7, 40), new ScheduleTime(7, 55));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("아침 약속"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Monday, timeRange),
            },
            PersonalScheduleDetails.CreateEmpty());
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    new PersonalScheduleEntry(schedule, schedule.TimeRanges[0]),
                }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid boardGrid = findRequiredControl<Grid>(scheduleBoard, "BoardGrid");
            Button scheduleCard = boardGrid.Children.OfType<Button>().Single();
            string[] labels = boardGrid.Children.OfType<TextBlock>().Select(getTextOrEmpty).ToArray();

            Assert.True(scheduleCard.Bounds.Height >= 24.0);
            Assert.Contains("07:00", labels);
            Assert.Contains("월요일 07:40–07:55", AutomationProperties.GetName(scheduleCard));
            Assert.Equal(new ScheduleBoardTimeBoundary(390), scheduleBoard.RenderedLayout.TimeAxis.Start);
            Assert.Equal(new ScheduleBoardTimeBoundary(480), scheduleBoard.RenderedLayout.TimeAxis.End);
            Assert.Equal(18, scheduleBoard.RenderedLayout.TimeAxis.IncrementCount);
            Assert.Equal(2, scheduleBoard.RenderedLayout.TimeAxis.GuideTimes.Count);
            Assert.Single(scheduleBoard.RenderedLayout.TimeAxis.LabelTimes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShortRepeatedScheduleUsesCompactUniqueCardsWithoutAnExportLegend()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(12, 20), new ScheduleTime(12, 35));
        PersonalScheduleDetails details = new PersonalScheduleDetails(new PersonalScheduleSection("A"), new PersonalScheduleInstructor("김교수"), new PersonalScheduleLocation("느헤미야홀 101호"));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("짧은 랩 미팅"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Tuesday, timeRange),
                new WeeklyTimeRange(EDay.Thursday, timeRange),
            },
            details);
        ScheduleEntry[] entries = schedule.TimeRanges
            .Select(range => (ScheduleEntry)new PersonalScheduleEntry(schedule, range))
            .ToArray();
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(entries));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button[] cards = findRequiredControl<Grid>(scheduleBoard, "BoardGrid").Children.OfType<Button>().ToArray();
            Assert.Equal(2, cards.Length);
            Assert.All(cards, card => Assert.Contains("compact", card.Classes));
            Assert.All(cards, card => Assert.True(card.Bounds.Height >= 24.0));
            Assert.All(
                cards,
                card =>
                {
                    TextBlock title = Assert.IsType<TextBlock>(card.Content);
                    Assert.Equal("짧은 랩 미팅(A)", title.Text);
                    Assert.Equal(14.0, title.FontSize);
                    Assert.Equal(18.0, title.LineHeight);
                    Assert.Equal(FontWeight.Bold, title.FontWeight);
                    Assert.Equal(1, title.MaxLines);
                    Assert.Equal(TextAlignment.Center, title.TextAlignment);
                });
            Assert.Equal(2, cards.Select(AutomationProperties.GetAutomationId).Distinct().Count());
            Assert.All(
                cards,
                card => Assert.Contains(
                    "분반 A",
                    AutomationProperties.GetName(card)));

            string[] exportTexts = scheduleBoard.PngExportSurface.GetVisualDescendants().OfType<TextBlock>().Select(getTextOrEmpty).ToArray();
            Assert.DoesNotContain("개인 일정 세부 정보", exportTexts);
            Assert.Contains("테스트 계획", exportTexts);
            Assert.DoesNotContain("한동대학교 · 2026-2", exportTexts);
            Assert.Equal(
                2,
                exportTexts.Count(text => text == "짧은 랩 미팅(A)"));
            Assert.DoesNotContain(
                exportTexts,
                text => text.Contains("분반 A", StringComparison.Ordinal)
                    || text.Contains("김교수", StringComparison.Ordinal)
                    || text.Contains("느헤미야홀 101호", StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task UnsatisfiedCourseConstraintsShowAReadOnlyPersonalPreviewAsync()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            addPersonalSchedule(
                workspace,
                "월요일 고정 일정",
                EDay.Monday,
                new ScheduleTime(8, 30),
                new ScheduleTime(9, 45));
            addPersonalSchedule(
                workspace,
                "화요일 고정 일정",
                EDay.Tuesday,
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 45));

            await workspace.RecommendationRefreshTask;

            Assert.True(workspace.HasUnsatisfiedScheduleConstraints);
            Assert.False(workspace.HasRecommendations);
            Assert.True(workspace.HasScheduleEntries);
            Assert.False(workspace.CanExportSchedule);
            Assert.Empty(workspace.ActiveRecommendation.Entries);
            Assert.NotEmpty(workspace.DisplayedSchedule.Entries);
            Assert.True(workspace.HasUnsatisfiedPersonalSchedulePreview);
        }
    }

}
