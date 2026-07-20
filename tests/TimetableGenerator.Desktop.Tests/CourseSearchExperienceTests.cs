using System;
using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class CourseSearchExperienceTests
{
    [AvaloniaFact]
    public void SearchClassifiesEverySupportedMatchKind()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");

            assertMatchKind(
                programming,
                "CSE10001",
                ECourseSearchMatchKind.ExactCourseCode);
            assertMatchKind(
                programming,
                "CSE10",
                ECourseSearchMatchKind.CourseCodePrefix);
            assertMatchKind(
                programming,
                "프로그래밍 I",
                ECourseSearchMatchKind.ExactCourseTitle);
            assertMatchKind(
                programming,
                "프로그래밍",
                ECourseSearchMatchKind.CourseTitlePrefix);
            assertMatchKind(
                programming,
                "그래밍",
                ECourseSearchMatchKind.CourseTitleContains);
            assertMatchKind(
                programming,
                "홍길동",
                ECourseSearchMatchKind.InstructorContains);

            CourseSearchMatch? missingMatchOrNull =
                programming.FindSearchMatchOrNull(
                    CourseSearchQuery.Create("존재하지 않는 검색어"));
            Assert.Null(missingMatchOrNull);
        }
    }

    [AvaloniaFact]
    public void SearchIgnoresCaseAndUnicodeWhitespaceWithoutFuzzyMatching()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");

            assertMatchKind(
                programming,
                " c s e\u00A01 0 0 0 1 ",
                ECourseSearchMatchKind.ExactCourseCode);
            assertMatchKind(
                programming,
                "프 로\u2003그 래 밍 I",
                ECourseSearchMatchKind.ExactCourseTitle);
            assertMatchKind(
                programming,
                " pRoGrAmMiNg\u202FI ",
                ECourseSearchMatchKind.ExactCourseTitle);
            assertMatchKind(
                programming,
                "그 래 밍",
                ECourseSearchMatchKind.CourseTitleContains);

            CourseSearchMatch? typoMatchOrNull =
                programming.FindSearchMatchOrNull(
                    CourseSearchQuery.Create("Programing I"));
            CourseSearchMatch? reorderedMatchOrNull =
                programming.FindSearchMatchOrNull(
                    CourseSearchQuery.Create("I Programming"));
            Assert.Null(typoMatchOrNull);
            Assert.Null(reorderedMatchOrNull);

            workspace.SearchText = "\u00A0 \u2003 \u202F";
            Assert.Equal(2, workspace.VisibleCourses.Count);
        }
    }

    [AvaloniaFact]
    public void SearchResultsUseStableCodeAndTitleOrderForEqualMatches()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(
                CatalogProjectionTestFixture.CreateReorderedDocument()))
        {
            workspace.SearchText = "i";

            Assert.Collection(
                workspace.VisibleCourses,
                first => Assert.Equal("BFT30009", first.Code),
                second => Assert.Equal("CSE10001", second.Code));
        }
    }

    [AvaloniaFact]
    public void EmptySearchStateResetsQueryAndFiltersTogether()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SelectedUnitFilter = workspace.UnitFilters[1];
            workspace.SelectedRequirementFilter = workspace.RequirementFilters[1];
            workspace.SearchText = "존재하지 않는 검색어";

            Assert.Equal(
                ECourseSearchResultState.Empty,
                workspace.CourseSearchResultState);
            Assert.False(workspace.HasVisibleCourses);
            Assert.True(workspace.HasNoVisibleCourses);
            Assert.Equal("검색 결과 (0개)", workspace.VisibleCourseHeading);

            workspace.ResetCourseSearchCommand.Execute(null);

            Assert.Empty(workspace.SearchText);
            Assert.Same(workspace.UnitFilters[0], workspace.SelectedUnitFilter);
            Assert.Same(
                workspace.RequirementFilters[0],
                workspace.SelectedRequirementFilter);
            Assert.Equal(
                ECourseSearchResultState.Populated,
                workspace.CourseSearchResultState);
            Assert.True(workspace.HasVisibleCourses);
            Assert.False(workspace.HasNoVisibleCourses);
            Assert.Equal(2, workspace.VisibleCourses.Count);
        }
    }

    [AvaloniaFact]
    public void SelectedCourseCanBeReversedFromItsSearchResult()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");
            Assert.True(programming.IsAdded);

            workspace.EditOrRemoveSelectedCourseCommand.Execute(programming);

            Assert.False(programming.IsAdded);
            Assert.Empty(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Empty(workspace.ActivePlan.CourseChoiceGroups);
        }
    }

    [AvaloniaFact]
    public void TimeNotProvidedCourseCanBeReversedFromItsSearchResult()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.ActivePlan = workspace.Plans[1];
            CourseSearchItem seminar = workspace.VisibleCourses.Single(
                course => course.Code == "BFT30009");
            Assert.True(seminar.HasMultipleSelectionOptions);
            Assert.False(seminar.IsDirectAddButtonVisible);
            Assert.True(seminar.IsSelectionButtonVisible);
            CourseSelectionOption selectedOption = seminar.SelectionOptions[1];

            workspace.AddCourseSelectionOptionCommand.Execute(selectedOption);
            Assert.True(seminar.IsAdded);
            Assert.Equal(
                selectedOption.Selection.GetTimeNotProvidedOfferingId(),
                Assert.Single(
                    workspace.ActivePlan.Plan.UnscheduledOfferingSelections)
                    .OfferingId);
            Assert.False(seminar.IsSelectionButtonVisible);

            workspace.EditOrRemoveSelectedCourseCommand.Execute(seminar);

            Assert.False(seminar.IsAdded);
            Assert.Empty(
                workspace.ActivePlan.Plan.UnscheduledOfferingSelections);
        }
    }

    [AvaloniaFact]
    public void AlternativeCourseSelectionOpensEditingInsteadOfRemovingTheGroup()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(
                CatalogProjectionTestFixture
                    .CreateDocumentWithScheduledAlternativeCourse()))
        {
            workspace.ActivePlan = workspace.Plans[1];
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");
            CourseSearchItem seminar = workspace.VisibleCourses.Single(
                course => course.Code == "BFT30009");

            workspace.AddCourseCommand.Execute(programming);
            CourseChoiceDraftCourseItem programmingDraft = Assert.Single(
                workspace.CourseChoiceDraftCourses);
            programmingDraft.Offerings[0].SelectPreferredCommand.Execute(null);
            workspace.AlternativeCourseSearchText = "세미나";
            CourseChoiceAlternativeSearchItem alternative = Assert.Single(
                workspace.AlternativeCourseSearchResults);
            workspace.AddAlternativeCourseCommand.Execute(alternative);
            CourseChoiceDraftCourseItem seminarDraft = workspace
                .CourseChoiceDraftCourses
                .Single(candidate => candidate.CourseId == seminar.CourseId);
            seminarDraft.Offerings[0].SelectAcceptableCommand.Execute(null);
            workspace.SaveCourseChoiceCommand.Execute(null);

            Assert.True(programming.IsAdded);
            Assert.True(seminar.IsAdded);
            Assert.Single(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Contains(
                "수강 선택 수정",
                seminar.SelectedCourseActionAccessibleName,
                StringComparison.Ordinal);

            workspace.EditOrRemoveSelectedCourseCommand.Execute(seminar);

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.Single(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
        }
    }

    [AvaloniaFact]
    public void EmptyStateAndSelectedActionRemainAccessibleAndAligned()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseBrowserView courseBrowser = new CourseBrowserView();
            courseBrowser.DataContext = workspace;
            Window window = new Window();
            window.Width = 340.0;
            window.Height = 720.0;
            window.Content = courseBrowser;

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Button selectedAction = courseBrowser.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(
                        candidate => ReferenceEquals(
                            candidate.Command,
                            workspace.EditOrRemoveSelectedCourseCommand)
                        && candidate.IsVisible);
                Assert.Equal(
                    VerticalAlignment.Center,
                    selectedAction.VerticalContentAlignment);
                Assert.Contains(
                    "시간표에서 제거",
                    AutomationProperties.GetName(selectedAction),
                    StringComparison.Ordinal);

                workspace.SearchText = "존재하지 않는 검색어";
                Dispatcher.UIThread.RunJobs();

                ListBox results = findRequiredControl<ListBox>(
                    courseBrowser,
                    "CourseResultsList");
                StackPanel emptyState = findRequiredControl<StackPanel>(
                    courseBrowser,
                    "NoCourseResultsState");
                Button resetButton = findRequiredControl<Button>(
                    courseBrowser,
                    "ResetCourseSearchButton");

                Assert.False(results.IsVisible);
                Assert.True(emptyState.IsVisible);
                Assert.Equal(
                    "검색 결과 없음",
                    AutomationProperties.GetName(emptyState));
                Assert.Equal(
                    VerticalAlignment.Center,
                    resetButton.VerticalContentAlignment);
                Assert.Equal(
                    "검색 및 필터 초기화",
                    AutomationProperties.GetName(resetButton));
                Assert.Contains(
                    courseBrowser.GetVisualDescendants().OfType<TextBlock>(),
                    candidate => candidate.IsVisible
                        && candidate.Text == "일치하는 과목이 없습니다");
            }
            finally
            {
                window.Close();
            }
        }
    }

    private static void assertMatchKind(
        CourseSearchItem course,
        string queryText,
        ECourseSearchMatchKind expectedKind)
    {
        CourseSearchMatch? matchOrNull = course.FindSearchMatchOrNull(
            CourseSearchQuery.Create(queryText));
        Assert.NotNull(matchOrNull);
        if (matchOrNull == null)
        {
            throw new InvalidOperationException(
                "The expected course search match was not created.");
        }

        Assert.Equal(expectedKind, matchOrNull.Kind);
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string name)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(name);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("Required control not found: " + name);
        }

        return controlOrNull;
    }
}
