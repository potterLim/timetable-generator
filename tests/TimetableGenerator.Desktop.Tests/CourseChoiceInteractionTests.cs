using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class CourseChoiceInteractionTests
{
    [AvaloniaFact]
    public void SingleScheduledOfferingIsAddedAsAnAcceptableSingleton()
    {
        PlannerWorkspaceViewModel workspace = createChoiceWorkspace();

        try
        {
            CourseSearchItem seminar = findCourse(workspace, "세미나");

            Assert.Equal(1, seminar.ScheduledOfferingCount);
            Assert.False(seminar.IsSelectedOptionTimeNotProvided);

            workspace.AddCourseCommand.Execute(seminar);

            Assert.False(workspace.IsCourseChoiceEditorVisible);
            CourseChoiceGroup group = Assert.Single(
                workspace.ActivePlan.Plan.CourseChoiceGroups);
            CourseCandidate courseCandidate = Assert.Single(group.CourseCandidates);
            OfferingCandidate offeringCandidate = Assert.Single(
                courseCandidate.OfferingCandidates);
            Assert.Equal(ECourseChoiceCardinality.ExactlyOne, group.Cardinality);
            Assert.Equal(seminar.CourseId, courseCandidate.CourseId);
            Assert.Equal(EOfferingPreference.Acceptable, offeringCandidate.Preference);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void MultipleOfferingsStartExcludedAndRequireAnEligiblePreference()
    {
        PlannerWorkspaceViewModel workspace = createChoiceWorkspace();

        try
        {
            CourseSearchItem programming = findCourse(workspace, "프로그래밍");
            workspace.AddCourseCommand.Execute(programming);

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.False(workspace.HasAlternativeCourseChoices);
            Assert.Equal(string.Empty, workspace.CourseChoiceEditorDescription);
            CourseChoiceDraftCourseItem draft = Assert.Single(
                workspace.CourseChoiceDraftCourses);
            Assert.Equal(2, draft.Offerings.Count);
            Assert.All(draft.Offerings, offering => Assert.True(offering.IsExcluded));
            Assert.False(workspace.CanSaveCourseChoice);
            Assert.False(workspace.SaveCourseChoiceCommand.CanExecute(null));

            CourseOfferingPreferenceItem preferredOffering = draft.Offerings[0];
            CourseOfferingPreferenceItem excludedOffering = draft.Offerings[1];
            Assert.Equal("교수 정보 없음", excludedOffering.InstructorDisplayText);
            Assert.Equal("강의실 미정", excludedOffering.LocationDisplayText);
            Assert.Equal(
                "교수 정보 없음 · 강의실 미정",
                excludedOffering.LogisticsDisplayText);
            preferredOffering.SelectPreferredCommand.Execute(null);
            preferredOffering.SelectPreferredCommand.Execute(null);
            excludedOffering.SelectAcceptableCommand.Execute(null);

            Assert.True(preferredOffering.IsPreferred);
            Assert.True(excludedOffering.IsAcceptable);
            Assert.True(workspace.CanSaveCourseChoice);

            excludedOffering.SelectExcludedCommand.Execute(null);
            Assert.True(excludedOffering.IsExcluded);
            Assert.True(workspace.SaveCourseChoiceCommand.CanExecute(null));
            workspace.SaveCourseChoiceCommand.Execute(null);

            CourseChoiceGroup group = Assert.Single(
                workspace.ActivePlan.Plan.CourseChoiceGroups);
            CourseCandidate candidate = Assert.Single(group.CourseCandidates);
            Assert.Collection(
                candidate.OfferingCandidates,
                offering => Assert.Equal(
                    EOfferingPreference.Preferred,
                    offering.Preference),
                offering => Assert.Equal(
                    EOfferingPreference.Excluded,
                    offering.Preference));
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void AlternativeCoursesSaveEditAndDeleteAsOneExactlyOneGroup()
    {
        PlannerWorkspaceViewModel workspace = createChoiceWorkspace();

        try
        {
            CourseSearchItem programming = findCourse(workspace, "프로그래밍");
            workspace.AddCourseCommand.Execute(programming);
            CourseChoiceDraftCourseItem programmingDraft = Assert.Single(
                workspace.CourseChoiceDraftCourses);
            programmingDraft.Offerings[0].SelectPreferredCommand.Execute(null);

            workspace.AlternativeCourseSearchText = "세미나";
            CourseChoiceAlternativeSearchItem seminarSearchResult = Assert.Single(
                workspace.AlternativeCourseSearchResults);
            workspace.AddAlternativeCourseCommand.Execute(seminarSearchResult);

            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
            Assert.True(workspace.HasAlternativeCourseChoices);
            Assert.Equal(
                "과목별 분반을 정하면 이 중 한 과목만 추천합니다.",
                workspace.CourseChoiceEditorDescription);
            Assert.Empty(workspace.AlternativeCourseSearchResults);
            CourseChoiceDraftCourseItem seminarDraft = workspace
                .CourseChoiceDraftCourses
                .Single(candidate => candidate.Name == "세미나 3");
            Assert.Contains(
                "프로그래밍 I",
                programmingDraft.Offerings[0].PreferredButtonAccessibleName,
                StringComparison.Ordinal);
            Assert.Contains(
                "세미나 3",
                seminarDraft.Offerings[0].PreferredButtonAccessibleName,
                StringComparison.Ordinal);
            Assert.NotEqual(
                programmingDraft.Offerings[0].PreferredButtonAccessibleName,
                seminarDraft.Offerings[0].PreferredButtonAccessibleName);
            Assert.All(
                seminarDraft.Offerings,
                offering => Assert.True(offering.IsExcluded));
            Assert.False(workspace.CanSaveCourseChoice);

            seminarDraft.Offerings[0].SelectAcceptableCommand.Execute(null);
            workspace.SaveCourseChoiceCommand.Execute(null);

            CourseChoiceGroup savedGroup = Assert.Single(
                workspace.ActivePlan.Plan.CourseChoiceGroups);
            CourseChoiceGroupId savedGroupId = savedGroup.Id;
            Assert.Equal(ECourseChoiceCardinality.ExactlyOne, savedGroup.Cardinality);
            Assert.Equal(2, savedGroup.CourseCandidates.Count);
            Assert.Equal(2, Assert.Single(
                workspace.ActivePlan.CourseChoiceGroups).Courses.Count);

            PlanCourseChoiceGroupItem savedItem = Assert.Single(
                workspace.ActivePlan.CourseChoiceGroups);
            workspace.BeginEditCourseChoiceGroupCommand.Execute(savedItem);

            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
            CourseChoiceDraftCourseItem restoredProgramming = workspace
                .CourseChoiceDraftCourses
                .Single(candidate => candidate.Name == "프로그래밍 I");
            CourseChoiceDraftCourseItem restoredSeminar = workspace
                .CourseChoiceDraftCourses
                .Single(candidate => candidate.Name == "세미나 3");
            Assert.True(restoredProgramming.Offerings[0].IsPreferred);
            Assert.True(restoredProgramming.Offerings[1].IsExcluded);
            Assert.True(restoredSeminar.Offerings[0].IsAcceptable);

            restoredProgramming.Offerings[0].SelectAcceptableCommand.Execute(null);
            workspace.SaveCourseChoiceCommand.Execute(null);

            CourseChoiceGroup updatedGroup = Assert.Single(
                workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Equal(savedGroupId, updatedGroup.Id);
            CourseCandidate updatedProgramming = updatedGroup.CourseCandidates
                .Single(candidate => candidate.CourseId == programming.CourseId);
            Assert.Equal(
                EOfferingPreference.Acceptable,
                updatedProgramming.OfferingCandidates[0].Preference);

            PlanCourseChoiceGroupItem updatedItem = Assert.Single(
                workspace.ActivePlan.CourseChoiceGroups);
            workspace.RemoveCourseChoiceGroupCommand.Execute(updatedItem);

            Assert.Empty(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Empty(workspace.ActivePlan.CourseChoiceGroups);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PlanSwitchRestoresEachTimeNotProvidedOfferingSelection()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();

        try
        {
            CourseSearchItem seminar = findCourse(workspace, "세미나");
            Assert.Equal(2, seminar.SelectionOptions.Count);
            Assert.Equal(
                "세미나 3, 추가할 분반 선택",
                seminar.SelectionAccessibleName);
            CourseSelectionOption firstOffering = seminar.SelectionOptions[0];
            CourseSelectionOption secondOffering = seminar.SelectionOptions[1];

            seminar.SelectedSelectionOption = firstOffering;
            workspace.AddCourseCommand.Execute(seminar);

            workspace.ActivePlan = workspace.Plans[1];
            seminar.SelectedSelectionOption = secondOffering;
            workspace.AddCourseCommand.Execute(seminar);

            workspace.ActivePlan = workspace.Plans[0];
            Assert.True(seminar.IsAdded);
            Assert.Same(firstOffering, seminar.SelectedSelectionOption);

            workspace.ActivePlan = workspace.Plans[1];
            Assert.True(seminar.IsAdded);
            Assert.Same(secondOffering, seminar.SelectedSelectionOption);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CenteredEditorTrapsFocusAndEscapeRestoresTheInvokingAction()
    {
        PlannerWorkspaceViewModel workspace = createChoiceWorkspace();
        workspace.SearchText = "프로그래밍";
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1_200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);
            Button addButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => ReferenceEquals(
                        candidate.Command,
                        workspace.AddCourseCommand)
                    && ReferenceEquals(candidate.CommandParameter, programming));
            Assert.True(addButton.Focus());
            addButton.Command?.Execute(addButton.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Border overlay = findRequiredControl<Border>(
                host,
                "CourseChoiceEditorOverlay");
            Border dialog = findRequiredControl<Border>(
                host,
                "CourseChoiceEditorDialog");
            Grid workspaceSurface = findRequiredControl<Grid>(
                host,
                "WorkspaceSurface");
            RadioButton[] preferenceButtons = host.GetVisualDescendants()
                .OfType<RadioButton>()
                .Where(
                    candidate => candidate.Classes.Contains(
                        "preference-choice"))
                .ToArray();
            RadioButton firstPreferenceButton = preferenceButtons[0];

            Assert.True(overlay.IsVisible);
            Assert.False(workspaceSurface.IsEnabled);
            Assert.True(firstPreferenceButton.IsKeyboardFocusWithin);
            Assert.Equal(
                KeyboardNavigationMode.Cycle,
                KeyboardNavigation.GetTabNavigation(dialog));
            Assert.Equal(
                "수강 선택 대화상자",
                AutomationProperties.GetName(dialog));
            Assert.Equal(6, preferenceButtons.Length);
            Assert.Equal("선호", preferenceButtons[0].Content);
            Assert.Equal(false, preferenceButtons[0].IsChecked);
            Assert.Equal("가능", preferenceButtons[1].Content);
            Assert.Equal(false, preferenceButtons[1].IsChecked);
            Assert.Equal("제외", preferenceButtons[2].Content);
            Assert.Equal(true, preferenceButtons[2].IsChecked);
            Assert.Equal(
                "프로그래밍 I, 01분반, 선호",
                AutomationProperties.GetName(preferenceButtons[0]));
            Assert.Equal(
                "추천에서 가장 먼저 사용합니다.",
                AutomationProperties.GetHelpText(preferenceButtons[0]));
            assertBrushUsesResource(
                preferenceButtons[1].BorderBrush,
                "ControlBorderBrush",
                window.ActualThemeVariant);
            assertBrushUsesResource(
                preferenceButtons[0].BorderBrush,
                "ProductFocusStrokeBrush",
                window.ActualThemeVariant);
            Assert.Equal(new Thickness(2.0), preferenceButtons[0].BorderThickness);
            TextBlock preferenceGuidance = Assert.Single(
                host.GetVisualDescendants()
                    .OfType<TextBlock>(),
                candidate => candidate.Text
                    == "선호는 먼저 추천하고, 가능은 충돌할 때 사용합니다.");
            Assert.True(preferenceGuidance.IsVisible);
            Assert.Equal(new Thickness(0.0, 6.0, 0.0, 8.0), preferenceGuidance.Margin);
            Assert.Equal(17.0, preferenceGuidance.LineHeight);

            Assert.True(preferenceButtons[2].Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            assertBrushUsesResource(
                preferenceButtons[2].BorderBrush,
                "ProductFocusStrokeBrush",
                window.ActualThemeVariant);
            Assert.Equal(new Thickness(2.0), preferenceButtons[2].BorderThickness);

            preferenceButtons[0].Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(true, preferenceButtons[0].IsChecked);
            Assert.Equal(false, preferenceButtons[2].IsChecked);
            assertBrushUsesResource(
                preferenceButtons[0].Background,
                "PreferencePreferredFillBrush",
                window.ActualThemeVariant);
            assertBrushUsesResource(
                preferenceButtons[0].Foreground,
                "PreferencePreferredForegroundBrush",
                window.ActualThemeVariant);
            assertBrushUsesResource(
                preferenceButtons[0].BorderBrush,
                "PreferencePreferredBorderBrush",
                window.ActualThemeVariant);
            Assert.True(preferenceButtons[0].Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            assertBrushUsesResource(
                preferenceButtons[0].BorderBrush,
                "ProductFocusOnFillStrokeBrush",
                window.ActualThemeVariant);

            window.KeyPress(
                Key.Escape,
                RawInputModifiers.None,
                PhysicalKey.Escape,
                string.Empty);
            Dispatcher.UIThread.RunJobs();

            Assert.False(overlay.IsVisible);
            Assert.True(workspaceSurface.IsEnabled);
            Assert.True(addButton.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static PlannerWorkspaceViewModel createChoiceWorkspace()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture
            .CreateDocumentWithScheduledAlternativeCourse();
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(document);
        workspace.ActivePlan = workspace.Plans[1];
        return workspace;
    }

    private static CourseSearchItem findCourse(
        PlannerWorkspaceViewModel workspace,
        string searchText)
    {
        workspace.SearchText = searchText;
        return Assert.Single(workspace.VisibleCourses);
    }

    private static void assertBrushUsesResource(
        IBrush? actualBrushOrNull,
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
        Assert.True(hasResource, "Missing brush resource: " + resourceKey);
        SolidColorBrush actualBrush = Assert.IsType<SolidColorBrush>(
            actualBrushOrNull);
        SolidColorBrush expectedBrush = Assert.IsType<SolidColorBrush>(
            resourceOrNull);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
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
