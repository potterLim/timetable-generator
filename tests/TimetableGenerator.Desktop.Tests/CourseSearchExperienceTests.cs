using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
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
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");

            assertMatchKind(programming, "CSE10001", ECourseSearchMatchKind.ExactCourseCode);
            assertMatchKind(programming, "CSE10", ECourseSearchMatchKind.CourseCodePrefix);
            assertMatchKind(programming, "프로그래밍 I", ECourseSearchMatchKind.ExactCourseTitle);
            assertMatchKind(programming, "프로그래밍", ECourseSearchMatchKind.CourseTitlePrefix);
            assertMatchKind(programming, "그래밍", ECourseSearchMatchKind.CourseTitleContains);
            assertMatchKind(programming, "홍길동", ECourseSearchMatchKind.InstructorContains);

            CourseSearchMatch? missingMatchOrNull = programming.FindSearchMatchOrNull(CourseSearchQuery.Create("존재하지 않는 검색어"));
            Assert.Null(missingMatchOrNull);
        }
    }

    [AvaloniaFact]
    public void SearchIgnoresCaseAndUnicodeWhitespaceWithoutFuzzyMatching()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");

            assertMatchKind(programming, " c s e\u00A01 0 0 0 1 ", ECourseSearchMatchKind.ExactCourseCode);
            assertMatchKind(programming, "프 로\u2003그 래 밍 I", ECourseSearchMatchKind.ExactCourseTitle);
            assertMatchKind(programming, " pRoGrAmMiNg\u202FI ", ECourseSearchMatchKind.ExactCourseTitle);
            assertMatchKind(programming, "그 래 밍", ECourseSearchMatchKind.CourseTitleContains);

            CourseSearchMatch? typoMatchOrNull = programming.FindSearchMatchOrNull(CourseSearchQuery.Create("Programing I"));
            CourseSearchMatch? reorderedMatchOrNull = programming.FindSearchMatchOrNull(CourseSearchQuery.Create("I Programming"));
            Assert.Null(typoMatchOrNull);
            Assert.Null(reorderedMatchOrNull);

            workspace.SearchText = "\u00A0 \u2003 \u202F";
            Assert.Equal(2, workspace.VisibleCourses.Count);
        }
    }

    [AvaloniaFact]
    public void SearchResultsUseStableCodeAndTitleOrderForEqualMatches()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateReorderedDocument()))
        {
            workspace.SearchText = "i";

            Assert.Collection(
                workspace.VisibleCourses,
                first => Assert.Equal("BFT30009", first.Code),
                second => Assert.Equal("CSE10001", second.Code));
        }
    }

    [AvaloniaFact]
    public void MainSearchPreservesResultWhenRefinementKeepsTheSameCourse()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SearchText = "프로그래";
            CourseSearchItem initialResult = Assert.Single(workspace.VisibleCourses);
            int collectionChangedCount = 0;
            workspace.VisibleCourses.CollectionChanged += delegate
            {
                ++collectionChangedCount;
            };

            workspace.SearchText = "프로그래밍";

            Assert.Equal(0, collectionChangedCount);
            Assert.Same(initialResult, Assert.Single(workspace.VisibleCourses));
        }
    }

    [AvaloniaFact]
    public void AlternativeSearchPreservesResultWhenRefinementKeepsTheSameCourse()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse()))
        {
            workspace.ActivePlan = workspace.Plans[1];
            workspace.SearchText = "프로그래밍";
            CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(programming);
            workspace.AlternativeCourseSearchText = "세미";
            CourseChoiceAlternativeSearchItem initialResult = Assert.Single(workspace.AlternativeCourseSearchResults);
            int collectionChangedCount = 0;
            workspace.AlternativeCourseSearchResults.CollectionChanged += delegate
            {
                ++collectionChangedCount;
            };

            workspace.AlternativeCourseSearchText = "세미나";

            Assert.Equal(0, collectionChangedCount);
            Assert.Same(initialResult, Assert.Single(workspace.AlternativeCourseSearchResults));
        }
    }

    [AvaloniaFact]
    public void MainSearchPublishesTheFirstPreeditImmediately()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspaceWithEmptyPlan(CatalogProjectionTestFixture.CreateDocument());
        CourseBrowserView courseBrowser = new CourseBrowserView();
        courseBrowser.DataContext = workspace;
        Window window = new Window();
        window.Width = 390.0;
        window.Height = 820.0;
        window.Content = courseBrowser;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox searchBox = findRequiredControl<TextBox>(courseBrowser, "CourseSearchBox");
            TextInputMethodClient textInputMethodClient = getTextInputMethodClient(searchBox);
            Assert.True(searchBox.Focus());
            Dispatcher.UIThread.RunJobs();

            textInputMethodClient.SetPreeditText("프");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("프", workspace.SearchText);
            Assert.Equal("CSE10001", Assert.Single(workspace.VisibleCourses).Code);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void MainSearchPublishesFirstReplacementPreeditImmediately()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspaceWithEmptyPlan(CatalogProjectionTestFixture.CreateDocument());
        workspace.SearchText = "세미나";
        CourseBrowserView courseBrowser = new CourseBrowserView();
        courseBrowser.DataContext = workspace;
        Window window = new Window();
        window.Width = 390.0;
        window.Height = 820.0;
        window.Content = courseBrowser;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox searchBox = findRequiredControl<TextBox>(courseBrowser, "CourseSearchBox");
            TextInputMethodClient textInputMethodClient = getTextInputMethodClient(searchBox);
            Assert.True(searchBox.Focus());
            searchBox.SelectAll();
            Dispatcher.UIThread.RunJobs();

            textInputMethodClient.SetPreeditText("프");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("프", workspace.SearchText);
            Assert.Equal("CSE10001", Assert.Single(workspace.VisibleCourses).Code);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void MainSearchTracksKoreanImeCompositionBeforeCommitAndAcceptsFirstClick()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspaceWithEmptyPlan(CatalogProjectionTestFixture.CreateKoreanImeSearchDocument());
        CourseBrowserView courseBrowser = new CourseBrowserView();
        courseBrowser.DataContext = workspace;
        Window window = new Window();
        window.Width = 390.0;
        window.Height = 820.0;
        window.Content = courseBrowser;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox searchBox = findRequiredControl<TextBox>(courseBrowser, "CourseSearchBox");
            TextInputMethodClient textInputMethodClient = getTextInputMethodClient(searchBox);
            searchBox.Text = "물리";
            searchBox.CaretIndex = searchBox.Text.Length;
            Dispatcher.UIThread.RunJobs();

            textInputMethodClient.SetPreeditText("ㅎ");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("물리ㅎ", workspace.SearchText);

            textInputMethodClient.SetPreeditText("하");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("물리하", workspace.SearchText);

            textInputMethodClient.SetPreeditText("학");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("물리학", workspace.SearchText);
            CourseSearchItem physics = Assert.Single(workspace.VisibleCourses);
            Assert.Equal("PHY10001", physics.Code);
            Button addButton = findButtonByAccessibleName(courseBrowser, physics.AddButtonAccessibleName);
            int collectionChangedCount = 0;
            workspace.VisibleCourses.CollectionChanged += delegate
            {
                ++collectionChangedCount;
            };
            Point addButtonCenter = findControlCenter(window, addButton);
            window.MouseMove(addButtonCenter, RawInputModifiers.None);
            window.MouseDown(addButtonCenter, MouseButton.Left, RawInputModifiers.None);

            textInputMethodClient.SetPreeditText(null);
            searchBox.Text = "물리학";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, collectionChangedCount);
            Assert.Same(physics, Assert.Single(workspace.VisibleCourses));
            Assert.Contains(addButton, courseBrowser.GetVisualDescendants().OfType<Button>());

            window.MouseUp(addButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(physics.IsAdded);
            Assert.Equal(1, workspace.ActivePlan.SelectedCourseCount);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void AlternativeSearchTracksKoreanImeCompositionBeforeCommit()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse());
        workspace.ActivePlan = workspace.Plans[1];
        workspace.SearchText = "프로그래밍";
        CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);
        workspace.AddCourseCommand.Execute(programming);
        CourseChoiceEditorView courseChoiceEditor = new CourseChoiceEditorView();
        courseChoiceEditor.DataContext = workspace;
        Window window = new Window();
        window.Width = 940.0;
        window.Height = 820.0;
        window.Content = courseChoiceEditor;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox searchBox = findRequiredControl<TextBox>(courseChoiceEditor, "AlternativeCourseSearchBox");
            TextInputMethodClient textInputMethodClient = getTextInputMethodClient(searchBox);
            searchBox.Text = "세미";
            searchBox.CaretIndex = searchBox.Text.Length;
            Dispatcher.UIThread.RunJobs();

            textInputMethodClient.SetPreeditText("나");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("세미나", workspace.AlternativeCourseSearchText);
            CourseChoiceAlternativeSearchItem seminar = Assert.Single(workspace.AlternativeCourseSearchResults);
            Button addButton = findButtonByAccessibleName(courseChoiceEditor, seminar.AddButtonAccessibleName);
            int collectionChangedCount = 0;
            workspace.AlternativeCourseSearchResults.CollectionChanged += delegate
            {
                ++collectionChangedCount;
            };
            Point addButtonCenter = findControlCenter(window, addButton);
            window.MouseMove(addButtonCenter, RawInputModifiers.None);
            window.MouseDown(addButtonCenter, MouseButton.Left, RawInputModifiers.None);

            textInputMethodClient.SetPreeditText(null);
            searchBox.Text = "세미나";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, collectionChangedCount);
            Assert.Same(seminar, Assert.Single(workspace.AlternativeCourseSearchResults));
            Assert.Contains(addButton, courseChoiceEditor.GetVisualDescendants().OfType<Button>());

            window.MouseUp(addButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
            Assert.Single(
                workspace.CourseChoiceDraftCourses,
                candidate => candidate.CourseId == seminar.CourseId);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CompositionAwareSearchInsertsAndCancelsPreeditAtTheCaret()
    {
        CompositionAwareSearchTextBox searchBox = new CompositionAwareSearchTextBox();
        Window window = new Window();
        window.Width = 390.0;
        window.Height = 160.0;
        window.Content = searchBox;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextInputMethodClient textInputMethodClient = getTextInputMethodClient(searchBox);
            searchBox.Text = "물학";
            searchBox.CaretIndex = 1;
            Dispatcher.UIThread.RunJobs();

            textInputMethodClient.SetPreeditText("리");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("물리학", searchBox.QueryText);

            textInputMethodClient.SetPreeditText(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("물학", searchBox.QueryText);

            searchBox.QueryText = "물리학";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("물리학", searchBox.Text);
            Assert.Equal(searchBox.Text.Length, searchBox.CaretIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MainAddButtonSurvivesQueryCommitBetweenPointerPressAndRelease()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse());
        workspace.ActivePlan = workspace.Plans[1];
        workspace.SearchText = "프로그래";
        CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);
        CourseBrowserView courseBrowser = new CourseBrowserView();
        courseBrowser.DataContext = workspace;
        Window window = new Window();
        window.Width = 390.0;
        window.Height = 820.0;
        window.Content = courseBrowser;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox searchBox = findRequiredControl<TextBox>(courseBrowser, "CourseSearchBox");
            Button addButton = findButtonByAccessibleName(courseBrowser, programming.AddButtonAccessibleName);
            Assert.True(searchBox.Focus());
            Dispatcher.UIThread.RunJobs();
            Point addButtonCenter = findControlCenter(window, addButton);
            window.MouseMove(addButtonCenter, RawInputModifiers.None);
            window.MouseDown(addButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            searchBox.Text = "프로그래밍";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("프로그래밍", workspace.SearchText);
            Assert.Same(programming, Assert.Single(workspace.VisibleCourses));
            Assert.Contains(addButton, courseBrowser.GetVisualDescendants().OfType<Button>());

            window.MouseUp(addButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.Single(workspace.CourseChoiceDraftCourses);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void AlternativeAddButtonSurvivesQueryCommitBetweenPointerPressAndRelease()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse());
        workspace.ActivePlan = workspace.Plans[1];
        workspace.SearchText = "프로그래밍";
        CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);
        workspace.AddCourseCommand.Execute(programming);
        workspace.AlternativeCourseSearchText = "세미";
        CourseChoiceAlternativeSearchItem seminar = Assert.Single(workspace.AlternativeCourseSearchResults);
        CourseChoiceEditorView courseChoiceEditor = new CourseChoiceEditorView();
        courseChoiceEditor.DataContext = workspace;
        Window window = new Window();
        window.Width = 940.0;
        window.Height = 820.0;
        window.Content = courseChoiceEditor;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox searchBox = findRequiredControl<TextBox>(courseChoiceEditor, "AlternativeCourseSearchBox");
            Button addButton = findButtonByAccessibleName(courseChoiceEditor, seminar.AddButtonAccessibleName);
            Assert.True(searchBox.Focus());
            Dispatcher.UIThread.RunJobs();
            Point addButtonCenter = findControlCenter(window, addButton);
            window.MouseMove(addButtonCenter, RawInputModifiers.None);
            window.MouseDown(addButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            searchBox.Text = "세미나";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("세미나", workspace.AlternativeCourseSearchText);
            Assert.Same(seminar, Assert.Single(workspace.AlternativeCourseSearchResults));
            Assert.Contains(addButton, courseChoiceEditor.GetVisualDescendants().OfType<Button>());

            window.MouseUp(addButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
            Assert.Single(
                workspace.CourseChoiceDraftCourses,
                candidate => candidate.CourseId == seminar.CourseId);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void EmptySearchStateResetsQueryAndFiltersTogether()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SelectedUnitFilter = workspace.UnitFilters[1];
            workspace.SelectedRequirementFilter = workspace.RequirementFilters[1];
            workspace.SearchText = "존재하지 않는 검색어";

            Assert.Equal(ECourseSearchResultState.Empty, workspace.CourseSearchResultState);
            Assert.False(workspace.HasVisibleCourses);
            Assert.True(workspace.HasNoVisibleCourses);
            Assert.Equal("검색 결과 (0개)", workspace.VisibleCourseHeading);

            workspace.ResetCourseSearchCommand.Execute(null);

            Assert.Empty(workspace.SearchText);
            Assert.Same(workspace.UnitFilters[0], workspace.SelectedUnitFilter);
            Assert.Same(workspace.RequirementFilters[0], workspace.SelectedRequirementFilter);
            Assert.Equal(ECourseSearchResultState.Populated, workspace.CourseSearchResultState);
            Assert.True(workspace.HasVisibleCourses);
            Assert.False(workspace.HasNoVisibleCourses);
            Assert.Equal(2, workspace.VisibleCourses.Count);
        }
    }

    [AvaloniaFact]
    public void SelectedMultiOfferingCourseOpensItsPreferenceEditor()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");
            Assert.True(programming.IsAdded);

            workspace.EditOrRemoveSelectedCourseCommand.Execute(programming);

            Assert.True(programming.IsAdded);
            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.Single(workspace.CourseChoiceDraftCourses);
            Assert.Equal(2, workspace.CourseChoiceDraftCourses[0].Offerings.Count);
        }
    }

    [AvaloniaFact]
    public void MultiTimeNotProvidedCourseOpensTheSharedPreferenceEditor()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.ActivePlan = workspace.Plans[1];
            CourseSearchItem seminar = workspace.VisibleCourses.Single(
                course => course.Code == "BFT30009");
            Assert.True(seminar.HasMultipleSelectionOptions);
            Assert.True(seminar.IsDirectAddButtonVisible);
            Assert.False(seminar.IsSelectionButtonVisible);

            workspace.AddCourseCommand.Execute(seminar);

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            CourseChoiceDraftCourseItem draft = Assert.Single(workspace.CourseChoiceDraftCourses);
            Assert.Equal(2, draft.Offerings.Count);
            Assert.All(draft.Offerings, offering => Assert.True(offering.IsAcceptable));
            Assert.False(seminar.IsSelectionButtonVisible);
        }
    }

    [AvaloniaFact]
    public void AlternativeCourseSelectionOpensEditingInsteadOfRemovingTheGroup()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse()))
        {
            workspace.ActivePlan = workspace.Plans[1];
            CourseSearchItem programming = workspace.VisibleCourses.Single(
                course => course.Code == "CSE10001");
            CourseSearchItem seminar = workspace.VisibleCourses.Single(
                course => course.Code == "BFT30009");

            workspace.AddCourseCommand.Execute(programming);
            CourseChoiceDraftCourseItem programmingDraft = Assert.Single(workspace.CourseChoiceDraftCourses);
            programmingDraft.Offerings[0].SelectPreferredCommand.Execute(null);
            workspace.AlternativeCourseSearchText = "세미나";
            CourseChoiceAlternativeSearchItem alternative = Assert.Single(workspace.AlternativeCourseSearchResults);
            workspace.AddAlternativeCourseCommand.Execute(alternative);
            CourseChoiceDraftCourseItem seminarDraft = workspace
                .CourseChoiceDraftCourses
                .Single(candidate => candidate.CourseId == seminar.CourseId);
            seminarDraft.Offerings[0].SelectAcceptableCommand.Execute(null);
            workspace.SaveCourseChoiceCommand.Execute(null);

            Assert.True(programming.IsAdded);
            Assert.True(seminar.IsAdded);
            Assert.Single(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Contains("수강 선택 수정", seminar.SelectedCourseActionAccessibleName, StringComparison.Ordinal);

            workspace.EditOrRemoveSelectedCourseCommand.Execute(seminar);

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.Single(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
        }
    }

    [AvaloniaFact]
    public void EmptyStateAndSelectedActionRemainAccessibleAndAligned()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
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
                Assert.Equal(VerticalAlignment.Center, selectedAction.VerticalContentAlignment);
                Assert.Contains("수강 선택 수정", AutomationProperties.GetName(selectedAction), StringComparison.Ordinal);

                workspace.SearchText = "존재하지 않는 검색어";
                Dispatcher.UIThread.RunJobs();

                ListBox results = findRequiredControl<ListBox>(courseBrowser, "CourseResultsList");
                StackPanel emptyState = findRequiredControl<StackPanel>(courseBrowser, "NoCourseResultsState");
                Button resetButton = findRequiredControl<Button>(courseBrowser, "ResetCourseSearchButton");

                Assert.False(results.IsVisible);
                Assert.True(emptyState.IsVisible);
                Assert.Equal("검색 결과 없음", AutomationProperties.GetName(emptyState));
                Assert.Equal(VerticalAlignment.Center, resetButton.VerticalContentAlignment);
                Assert.Equal("검색 및 필터 초기화", AutomationProperties.GetName(resetButton));
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

    private static void assertMatchKind(CourseSearchItem course, string queryText, ECourseSearchMatchKind expectedKind)
    {
        CourseSearchMatch? matchOrNull = course.FindSearchMatchOrNull(CourseSearchQuery.Create(queryText));
        Assert.NotNull(matchOrNull);
        if (matchOrNull == null)
        {
            throw new InvalidOperationException("The expected course search match was not created.");
        }

        Assert.Equal(expectedKind, matchOrNull.Kind);
    }

    private static TControl findRequiredControl<TControl>(Control root, string name)
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

    private static TextInputMethodClient getTextInputMethodClient(TextBox textBox)
    {
        TextInputMethodClientRequestedEventArgs eventArguments = new TextInputMethodClientRequestedEventArgs()
        {
            RoutedEvent = InputElement.TextInputMethodClientRequestedEvent,
        };
        textBox.RaiseEvent(eventArguments);

        Assert.NotNull(eventArguments.Client);
        if (eventArguments.Client == null)
        {
            throw new InvalidOperationException("The text input method client could not be resolved.");
        }

        return eventArguments.Client;
    }

    private static Button findButtonByAccessibleName(Control root, string accessibleName)
    {
        return root.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => candidate.IsVisible
                && string.Equals(
                    AutomationProperties.GetName(candidate),
                    accessibleName,
                    StringComparison.Ordinal));
    }

    private static Point findControlCenter(Window window, Control control)
    {
        Point? controlOriginOrNull = control.TranslatePoint(new Point(0.0, 0.0), window);
        Assert.NotNull(controlOriginOrNull);
        if (controlOriginOrNull == null)
        {
            throw new InvalidOperationException("The control position could not be resolved.");
        }

        return controlOriginOrNull.Value + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }
}
