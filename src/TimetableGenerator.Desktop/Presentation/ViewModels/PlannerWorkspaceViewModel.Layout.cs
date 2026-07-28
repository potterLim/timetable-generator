using System;
using System.Windows.Input;

using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Layout;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private static readonly WorkspacePaneWidth COLLAPSED_COURSE_PANE_WIDTH = new WorkspacePaneWidth(320.0);
    private static readonly WorkspacePaneWidth COLLAPSED_INSPECTOR_PANE_WIDTH = new WorkspacePaneWidth(304.0);
    private static readonly WorkspacePaneWidth EXTRA_WIDE_COURSE_PANE_WIDTH = new WorkspacePaneWidth(312.0);
    private static readonly WorkspacePaneWidth EXTRA_WIDE_INSPECTOR_PANE_WIDTH = new WorkspacePaneWidth(288.0);
    private static readonly WorkspacePaneWidth WIDE_COURSE_PANE_WIDTH = new WorkspacePaneWidth(312.0);
    private static readonly WorkspacePaneWidth WIDE_INSPECTOR_PANE_WIDTH = new WorkspacePaneWidth(304.0);

    private EWorkspaceLayoutMode mLayoutMode;

    private bool mHasAppliedWorkspaceLayout;

    private bool mIsCoursePaneOpen;

    private bool mIsInspectorPaneOpen;

    private SplitViewDisplayMode mCoursePaneDisplayMode;

    private SplitViewDisplayMode mInspectorPaneDisplayMode;

    private WorkspacePaneWidth mCoursePaneWidth;

    private WorkspacePaneWidth mInspectorPaneWidth;

    public EWorkspaceLayoutMode LayoutMode
    {
        get
        {
            return mLayoutMode;
        }
    }

    public bool IsCoursePaneOpen
    {
        get
        {
            return mIsCoursePaneOpen;
        }
        set
        {
            if (setProperty(ref mIsCoursePaneOpen, value))
            {
                raisePropertyChanged(nameof(IsCoursePaneToggleVisible));
                raisePropertyChanged(nameof(IsCoursePaneDismissActionVisible));
            }
        }
    }

    public bool IsInspectorPaneOpen
    {
        get
        {
            return mIsInspectorPaneOpen;
        }
        set
        {
            if (setProperty(ref mIsInspectorPaneOpen, value))
            {
                raisePropertyChanged(nameof(IsInspectorPaneToggleVisible));
                raisePropertyChanged(nameof(IsInspectorPaneDismissActionVisible));
            }
        }
    }

    public SplitViewDisplayMode CoursePaneDisplayMode
    {
        get
        {
            return mCoursePaneDisplayMode;
        }
    }

    public SplitViewDisplayMode InspectorPaneDisplayMode
    {
        get
        {
            return mInspectorPaneDisplayMode;
        }
    }

    public double CoursePaneWidth
    {
        get
        {
            return mCoursePaneWidth.Value;
        }
    }

    public bool UsesCourseOverlayPresentation
    {
        get
        {
            return CoursePaneDisplayMode == SplitViewDisplayMode.Overlay;
        }
    }

    public double InspectorPaneWidth
    {
        get
        {
            return mInspectorPaneWidth.Value;
        }
    }

    public bool UsesInspectorOverlayPresentation
    {
        get
        {
            return InspectorPaneDisplayMode == SplitViewDisplayMode.Overlay;
        }
    }

    public bool IsCoursePaneToggleVisible
    {
        get
        {
            return IsCoursePaneOpen == false;
        }
    }

    public bool IsCoursePaneDismissActionVisible
    {
        get
        {
            return IsCoursePaneOpen;
        }
    }

    public bool IsInspectorPaneToggleVisible
    {
        get
        {
            return IsInspectorPaneOpen == false;
        }
    }

    public bool IsInspectorPaneDismissActionVisible
    {
        get
        {
            return IsInspectorPaneOpen;
        }
    }

    public ICommand ToggleCoursePaneCommand { get; }

    public ICommand OpenInspectorPaneCommand { get; }

    public ICommand CloseInspectorPaneCommand { get; }

    internal void applyWorkspaceWidth(WorkspaceWidth workspaceWidth)
    {
        EWorkspaceLayoutMode newLayoutMode = WorkspaceLayoutPolicy.FindLayoutMode(workspaceWidth);
        bool isInitialLayout = mHasAppliedWorkspaceLayout == false;
        if (newLayoutMode == LayoutMode && isInitialLayout == false)
        {
            return;
        }

        mLayoutMode = newLayoutMode;
        configurePanePresentationForLayoutMode();
        if (isInitialLayout)
        {
            initializePaneOpenStates();
        }

        mHasAppliedWorkspaceLayout = true;
        raisePropertyChanged(nameof(LayoutMode));
    }

    internal bool tryCloseTopmostTransientWorkspaceOverlay()
    {
        if (IsCourseChoiceEditorVisible)
        {
            closeCourseChoiceEditingState();
            return true;
        }

        if (IsPersonalScheduleOverlayVisible)
        {
            closePersonalScheduleEditingState();
            return true;
        }

        if (IsPlanEditingOverlayVisible)
        {
            closePlanEditingState();
            return true;
        }

        if (CoursePaneDisplayMode == SplitViewDisplayMode.Overlay && IsCoursePaneOpen)
        {
            IsCoursePaneOpen = false;
            return true;
        }

        if (InspectorPaneDisplayMode == SplitViewDisplayMode.Overlay && IsInspectorPaneOpen)
        {
            IsInspectorPaneOpen = false;
            return true;
        }

        return false;
    }

    private void toggleCoursePane()
    {
        if (HasActivePlan == false)
        {
            return;
        }

        bool isOpeningCoursePane = IsCoursePaneOpen == false;
        IsCoursePaneOpen = isOpeningCoursePane;
    }

    private void openInspectorPane()
    {
        if (HasActivePlan == false)
        {
            return;
        }

        closeCoursePaneBeforeOpeningInspectorOverlay();
        IsInspectorPaneOpen = true;
    }

    private void closeInspectorPane()
    {
        IsInspectorPaneOpen = false;
    }

    private void updatePaneStateAfterPlanCollectionChanged(bool previouslyHadActivePlan)
    {
        if (HasActivePlan == false)
        {
            IsCoursePaneOpen = false;
            IsInspectorPaneOpen = false;
            return;
        }

        if (previouslyHadActivePlan == false)
        {
            IsCoursePaneOpen = LayoutMode != EWorkspaceLayoutMode.Compact;
            IsInspectorPaneOpen = false;
        }
    }

    private void configurePanePresentationForLayoutMode()
    {
        switch (LayoutMode)
        {
            case EWorkspaceLayoutMode.ExtraWide:
                setCoursePanePresentation(SplitViewDisplayMode.Inline, EXTRA_WIDE_COURSE_PANE_WIDTH);
                setInspectorPanePresentation(SplitViewDisplayMode.Inline, EXTRA_WIDE_INSPECTOR_PANE_WIDTH);
                break;
            case EWorkspaceLayoutMode.Wide:
                setCoursePanePresentation(SplitViewDisplayMode.Inline, WIDE_COURSE_PANE_WIDTH);
                setInspectorPanePresentation(SplitViewDisplayMode.Overlay, WIDE_INSPECTOR_PANE_WIDTH);
                break;
            case EWorkspaceLayoutMode.Medium:
                setCoursePanePresentation(SplitViewDisplayMode.Inline, COLLAPSED_COURSE_PANE_WIDTH);
                setInspectorPanePresentation(SplitViewDisplayMode.Overlay, COLLAPSED_INSPECTOR_PANE_WIDTH);
                break;
            case EWorkspaceLayoutMode.Compact:
                setCoursePanePresentation(SplitViewDisplayMode.Overlay, COLLAPSED_COURSE_PANE_WIDTH);
                setInspectorPanePresentation(SplitViewDisplayMode.Overlay, COLLAPSED_INSPECTOR_PANE_WIDTH);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(LayoutMode), LayoutMode, "Unknown workspace layout mode.");
        }
    }

    private void initializePaneOpenStates()
    {
        IsCoursePaneOpen = LayoutMode != EWorkspaceLayoutMode.Compact;
        IsInspectorPaneOpen = false;
    }

    private void closeCoursePaneBeforeOpeningInspectorOverlay()
    {
        bool areBothPanesOverlay = CoursePaneDisplayMode == SplitViewDisplayMode.Overlay && InspectorPaneDisplayMode == SplitViewDisplayMode.Overlay;
        if (areBothPanesOverlay)
        {
            IsCoursePaneOpen = false;
        }
    }

    private void setCoursePanePresentation(SplitViewDisplayMode displayMode, WorkspacePaneWidth paneWidth)
    {
        mCoursePaneDisplayMode = displayMode;
        mCoursePaneWidth = paneWidth;
        raisePropertyChanged(nameof(CoursePaneDisplayMode));
        raisePropertyChanged(nameof(CoursePaneWidth));
        raisePropertyChanged(nameof(UsesCourseOverlayPresentation));
    }

    private void setInspectorPanePresentation(SplitViewDisplayMode displayMode, WorkspacePaneWidth paneWidth)
    {
        mInspectorPaneDisplayMode = displayMode;
        mInspectorPaneWidth = paneWidth;
        raisePropertyChanged(nameof(InspectorPaneDisplayMode));
        raisePropertyChanged(nameof(InspectorPaneWidth));
        raisePropertyChanged(nameof(UsesInspectorOverlayPresentation));
    }
}
