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
    public void MultipleOfferingsStartAcceptableAndCanBeRefined()
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
            Assert.All(draft.Offerings, offering => Assert.True(offering.IsAcceptable));
            Assert.True(workspace.CanSaveCourseChoice);
            Assert.True(workspace.SaveCourseChoiceCommand.CanExecute(null));

            CourseOfferingPreferenceItem preferredOffering = draft.Offerings[0];
            CourseOfferingPreferenceItem alternativeOffering = draft.Offerings[1];
            Assert.Equal("교수 정보 없음", alternativeOffering.InstructorDisplayText);
            Assert.Equal("강의실 미정", alternativeOffering.LocationDisplayText);
            Assert.Equal(
                "교수 정보 없음 · 강의실 미정",
                alternativeOffering.LogisticsDisplayText);
            preferredOffering.SelectPreferredCommand.Execute(null);
            preferredOffering.SelectPreferredCommand.Execute(null);

            Assert.True(preferredOffering.IsPreferred);
            Assert.True(alternativeOffering.IsAcceptable);
            Assert.True(workspace.CanSaveCourseChoice);

            alternativeOffering.SelectExcludedCommand.Execute(null);
            Assert.True(alternativeOffering.IsExcluded);
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
            programmingDraft.Offerings[1].SelectExcludedCommand.Execute(null);

            workspace.AlternativeCourseSearchText = "세미나";
            CourseChoiceAlternativeSearchItem seminarSearchResult = Assert.Single(
                workspace.AlternativeCourseSearchResults);
            workspace.AddAlternativeCourseCommand.Execute(seminarSearchResult);

            Assert.Equal(2, workspace.CourseChoiceDraftCourses.Count);
            Assert.True(workspace.HasAlternativeCourseChoices);
            Assert.Equal(
                "과목별 분반을 정하면 각 조합에는 이 중 한 과목만 포함됩니다.",
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
                offering => Assert.True(offering.IsAcceptable));
            Assert.True(workspace.CanSaveCourseChoice);
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
            RadioButton selectedPreferenceButton = preferenceButtons[1];

            Assert.True(overlay.IsVisible);
            Assert.False(workspaceSurface.IsEnabled);
            Assert.True(selectedPreferenceButton.IsKeyboardFocusWithin);
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
            Assert.Equal(true, preferenceButtons[1].IsChecked);
            Assert.Equal("제외", preferenceButtons[2].Content);
            Assert.Equal(false, preferenceButtons[2].IsChecked);
            foreach (RadioButton preferenceButton in preferenceButtons)
            {
                assertPreferenceOutlineEnclosesControl(preferenceButton);
            }

            Assert.Equal(
                "프로그래밍 I, 01분반, 선호",
                AutomationProperties.GetName(preferenceButtons[0]));
            Assert.Equal(
                "조합에서 우선 사용",
                AutomationProperties.GetHelpText(preferenceButtons[0]));
            Assert.Equal(
                "조합 후보로 사용",
                AutomationProperties.GetHelpText(preferenceButtons[1]));
            Assert.Equal(
                "조합에서 사용하지 않음",
                AutomationProperties.GetHelpText(preferenceButtons[2]));
            Assert.Equal(
                "조합에서 우선 사용",
                ToolTip.GetTip(preferenceButtons[0]));
            Assert.Equal(
                "조합 후보로 사용",
                ToolTip.GetTip(preferenceButtons[1]));
            Assert.Equal(
                "조합에서 사용하지 않음",
                ToolTip.GetTip(preferenceButtons[2]));
            assertBrushUsesResource(
                preferenceButtons[2].BorderBrush,
                "ControlBorderBrush",
                window.ActualThemeVariant);
            assertBrushUsesResource(
                preferenceButtons[0].BorderBrush,
                "ControlBorderBrush",
                window.ActualThemeVariant);
            Assert.Equal(new Thickness(1.0), preferenceButtons[0].BorderThickness);
            assertBrushUsesResource(
                selectedPreferenceButton.BorderBrush,
                "SelectionIndicatorBrush",
                window.ActualThemeVariant);
            Assert.Equal(
                new Thickness(1.0),
                selectedPreferenceButton.BorderThickness);
            Assert.True(firstPreferenceButton.Focus());
            Assert.True(selectedPreferenceButton.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            assertBrushUsesResource(
                selectedPreferenceButton.BorderBrush,
                "ProductFocusStrokeBrush",
                window.ActualThemeVariant);
            Assert.Equal(
                new Thickness(2.0),
                selectedPreferenceButton.BorderThickness);

            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };
            RadioButton[] firstOfferingPreferenceButtons = preferenceButtons
                .Take(3)
                .ToArray();
            Button closeEditorButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name
                        == "CloseCourseChoiceEditorButton");
            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();

                foreach (RadioButton preferenceButton in firstOfferingPreferenceButtons)
                {
                    Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
                    preferenceButton.Command?.Execute(null);
                    movePointerOutsidePreferenceButtons(window);

                    Assert.Equal(true, preferenceButton.IsChecked);
                    Assert.Single(
                        firstOfferingPreferenceButtons,
                        candidate => candidate.IsChecked == true);
                    assertSelectedPreferenceVisuals(
                        preferenceButton,
                        themeVariant,
                        "SelectionSurfaceBrush");
                    assertPreferenceOutlineEnclosesControl(preferenceButton);
                    Assert.True(preferenceButton.Focus(NavigationMethod.Tab));
                    Dispatcher.UIThread.RunJobs();
                    assertSelectedPreferenceFocusVisuals(
                        preferenceButton,
                        themeVariant);
                    assertPreferenceOutlineEnclosesControl(preferenceButton);
                }

                Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
                RadioButton selectedExcludedPreferenceButton =
                    firstOfferingPreferenceButtons[2];
                Point selectedPreferenceCenter = findControlCenter(
                    window,
                    selectedExcludedPreferenceButton);
                window.MouseMove(
                    selectedPreferenceCenter,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertSelectedPreferenceVisuals(
                    selectedExcludedPreferenceButton,
                    themeVariant,
                    "SelectionHoverSurfaceBrush");
                assertPreferenceOutlineEnclosesControl(
                    selectedExcludedPreferenceButton);

                window.MouseDown(
                    selectedPreferenceCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertSelectedPreferenceVisuals(
                    selectedExcludedPreferenceButton,
                    themeVariant,
                    "SelectionPressedSurfaceBrush");
                window.MouseUp(
                    selectedPreferenceCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                RadioButton unselectedPreferenceButton =
                    firstOfferingPreferenceButtons[0];
                Point unselectedPreferenceCenter = findControlCenter(
                    window,
                    unselectedPreferenceButton);
                window.MouseMove(
                    unselectedPreferenceCenter,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertUnselectedPreferenceVisuals(
                    unselectedPreferenceButton,
                    themeVariant,
                    "HoverSurfaceBrush");
                assertPreferenceOutlineEnclosesControl(
                    unselectedPreferenceButton);
                window.MouseDown(
                    unselectedPreferenceCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertUnselectedPreferenceVisuals(
                    unselectedPreferenceButton,
                    themeVariant,
                    "PressedSurfaceBrush");
                window.MouseUp(
                    unselectedPreferenceCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }

            preferenceButtons[0].Command?.Execute(null);
            movePointerOutsidePreferenceButtons(window);
            Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
            Assert.True(preferenceButtons[0].Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            assertSelectedPreferenceFocusVisuals(
                preferenceButtons[0],
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

    private static void assertSelectedPreferenceVisuals(
        RadioButton preferenceButton,
        ThemeVariant themeVariant,
        string backgroundResourceKey)
    {
        assertBrushUsesResource(
            preferenceButton.Background,
            backgroundResourceKey,
            themeVariant);
        assertBrushUsesResource(
            preferenceButton.BorderBrush,
            "SelectionIndicatorBrush",
            themeVariant);
        assertBrushUsesResource(
            preferenceButton.Foreground,
            "TextPrimaryBrush",
            themeVariant);
        Assert.Equal(FontWeight.SemiBold, preferenceButton.FontWeight);
    }

    private static void assertSelectedPreferenceFocusVisuals(
        RadioButton preferenceButton,
        ThemeVariant themeVariant)
    {
        assertBrushUsesResource(
            preferenceButton.Background,
            "SelectionSurfaceBrush",
            themeVariant);
        assertBrushUsesResource(
            preferenceButton.BorderBrush,
            "ProductFocusStrokeBrush",
            themeVariant);
        assertBrushUsesResource(
            preferenceButton.Foreground,
            "TextPrimaryBrush",
            themeVariant);
        Assert.Equal(new Thickness(2.0), preferenceButton.BorderThickness);
        Assert.Equal(FontWeight.SemiBold, preferenceButton.FontWeight);
    }

    private static void assertUnselectedPreferenceVisuals(
        RadioButton preferenceButton,
        ThemeVariant themeVariant,
        string backgroundResourceKey)
    {
        Assert.Equal(false, preferenceButton.IsChecked);
        assertBrushUsesResource(
            preferenceButton.Background,
            backgroundResourceKey,
            themeVariant);
        assertBrushUsesResource(
            preferenceButton.BorderBrush,
            "ControlBorderBrush",
            themeVariant);
        assertBrushUsesResource(
            preferenceButton.Foreground,
            "TextPrimaryBrush",
            themeVariant);
        Assert.Equal(FontWeight.Normal, preferenceButton.FontWeight);
    }

    private static void assertPreferenceOutlineEnclosesControl(
        RadioButton preferenceButton)
    {
        Assert.True(preferenceButton.UseLayoutRounding);
        Border outline = preferenceButton.GetVisualDescendants()
            .OfType<Border>()
            .Single(candidate => candidate.Name == "PART_Outline");

        Assert.Equal(preferenceButton.Bounds.Width, outline.Bounds.Width);
        Assert.Equal(preferenceButton.Bounds.Height, outline.Bounds.Height);
        Assert.Equal(preferenceButton.BorderBrush, outline.BorderBrush);
        Assert.Equal(preferenceButton.BorderThickness, outline.BorderThickness);
        Assert.True(outline.BorderThickness.Left > 0.0);
        Assert.Equal(outline.BorderThickness.Left, outline.BorderThickness.Top);
        Assert.Equal(outline.BorderThickness.Left, outline.BorderThickness.Right);
        Assert.Equal(outline.BorderThickness.Left, outline.BorderThickness.Bottom);
    }

    private static void movePointerOutsidePreferenceButtons(Window window)
    {
        window.MouseMove(new Point(1.0, 1.0), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point findControlCenter(
        Window window,
        Control control)
    {
        Point? controlOriginOrNull = control.TranslatePoint(
            new Point(0.0, 0.0),
            window);
        Assert.NotNull(controlOriginOrNull);
        if (controlOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The preference control position could not be resolved.");
        }

        return controlOriginOrNull.Value
            + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
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
