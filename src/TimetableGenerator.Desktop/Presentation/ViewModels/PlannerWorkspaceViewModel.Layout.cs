using System;
using System.Windows.Input;

using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Layout;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private const double COLLAPSED_COURSE_PANE_WIDTH = 320.0;
    private const double COLLAPSED_INSPECTOR_PANE_WIDTH = 320.0;
    private const double EXTRA_WIDE_COURSE_PANE_WIDTH = 352.0;
    private const double EXTRA_WIDE_INSPECTOR_PANE_WIDTH = 312.0;
    private const double WIDE_COURSE_PANE_WIDTH = 328.0;
    private const double WIDE_INSPECTOR_PANE_WIDTH = 296.0;

    private EWorkspaceLayoutMode mLayoutMode;

    private bool mIsCoursePaneOpen;

    private bool mIsInspectorPaneOpen;

    private SplitViewDisplayMode mCoursePaneDisplayMode;

    private SplitViewDisplayMode mInspectorPaneDisplayMode;

    private double mCoursePaneWidth;

    private double mInspectorPaneWidth;

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
            setProperty(ref mIsCoursePaneOpen, value);
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
            setProperty(ref mIsInspectorPaneOpen, value);
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
            return mCoursePaneWidth;
        }
    }

    public double InspectorPaneWidth
    {
        get
        {
            return mInspectorPaneWidth;
        }
    }

    public bool IsCoursePaneToggleVisible
    {
        get
        {
            return LayoutMode == EWorkspaceLayoutMode.Compact;
        }
    }

    public bool IsInspectorPaneToggleVisible
    {
        get
        {
            return LayoutMode == EWorkspaceLayoutMode.Medium
                || LayoutMode == EWorkspaceLayoutMode.Compact;
        }
    }

    public ICommand ToggleCoursePaneCommand { get; }

    public ICommand ToggleInspectorPaneCommand { get; }

    internal void applyWorkspaceWidth(WorkspaceWidth workspaceWidth)
    {
        EWorkspaceLayoutMode newLayoutMode = WorkspaceLayoutPolicy.FindLayoutMode(
            workspaceWidth);
        if (newLayoutMode == LayoutMode)
        {
            return;
        }

        mLayoutMode = newLayoutMode;
        configurePanesForLayoutMode();
        raisePropertyChanged(nameof(LayoutMode));
        raisePropertyChanged(nameof(IsCoursePaneToggleVisible));
        raisePropertyChanged(nameof(IsInspectorPaneToggleVisible));
    }

    internal void closeOverlayPanes()
    {
        if (IsPlanEditingOverlayVisible)
        {
            closePlanEditingState();
            return;
        }

        if (CoursePaneDisplayMode == SplitViewDisplayMode.Overlay)
        {
            IsCoursePaneOpen = false;
        }

        if (InspectorPaneDisplayMode == SplitViewDisplayMode.Overlay)
        {
            IsInspectorPaneOpen = false;
        }
    }

    private void toggleCoursePane()
    {
        IsCoursePaneOpen = IsCoursePaneOpen == false;
    }

    private void toggleInspectorPane()
    {
        IsInspectorPaneOpen = IsInspectorPaneOpen == false;
    }

    private void configurePanesForLayoutMode()
    {
        switch (LayoutMode)
        {
            case EWorkspaceLayoutMode.ExtraWide:
                setCoursePaneState(
                    SplitViewDisplayMode.Inline,
                    EXTRA_WIDE_COURSE_PANE_WIDTH,
                    EPaneOpenState.Open);
                setInspectorPaneState(
                    SplitViewDisplayMode.Inline,
                    EXTRA_WIDE_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Open);
                break;
            case EWorkspaceLayoutMode.Wide:
                setCoursePaneState(
                    SplitViewDisplayMode.Inline,
                    WIDE_COURSE_PANE_WIDTH,
                    EPaneOpenState.Open);
                setInspectorPaneState(
                    SplitViewDisplayMode.Inline,
                    WIDE_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Open);
                break;
            case EWorkspaceLayoutMode.Medium:
                setCoursePaneState(
                    SplitViewDisplayMode.Inline,
                    COLLAPSED_COURSE_PANE_WIDTH,
                    EPaneOpenState.Open);
                setInspectorPaneState(
                    SplitViewDisplayMode.Overlay,
                    COLLAPSED_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Closed);
                break;
            case EWorkspaceLayoutMode.Compact:
                setCoursePaneState(
                    SplitViewDisplayMode.Overlay,
                    COLLAPSED_COURSE_PANE_WIDTH,
                    EPaneOpenState.Closed);
                setInspectorPaneState(
                    SplitViewDisplayMode.Overlay,
                    COLLAPSED_INSPECTOR_PANE_WIDTH,
                    EPaneOpenState.Closed);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(LayoutMode),
                    LayoutMode,
                    "Unknown workspace layout mode.");
        }
    }

    private void setCoursePaneState(
        SplitViewDisplayMode displayMode,
        double paneWidth,
        EPaneOpenState paneOpenState)
    {
        mCoursePaneDisplayMode = displayMode;
        mCoursePaneWidth = paneWidth;
        IsCoursePaneOpen = paneOpenState == EPaneOpenState.Open;
        raisePropertyChanged(nameof(CoursePaneDisplayMode));
        raisePropertyChanged(nameof(CoursePaneWidth));
    }

    private void setInspectorPaneState(
        SplitViewDisplayMode displayMode,
        double paneWidth,
        EPaneOpenState paneOpenState)
    {
        mInspectorPaneDisplayMode = displayMode;
        mInspectorPaneWidth = paneWidth;
        IsInspectorPaneOpen = paneOpenState == EPaneOpenState.Open;
        raisePropertyChanged(nameof(InspectorPaneDisplayMode));
        raisePropertyChanged(nameof(InspectorPaneWidth));
    }
}
