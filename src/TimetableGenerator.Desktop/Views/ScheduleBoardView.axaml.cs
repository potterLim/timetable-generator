using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView : UserControl
{
    private const double TIME_COLUMN_WIDTH = 72.0;
    private const double HEADER_ROW_HEIGHT = 42.0;
    private const double TIME_INCREMENT_ROW_HEIGHT = 8.0;
    private const double HOUR_GUIDE_EXTENSION_WIDTH = 8.0;
    private const double TIME_LABEL_WIDTH = 48.0;
    private const double TIME_LABEL_HEIGHT = 16.0;
    private const double TIME_LABEL_GUIDE_GAP = 10.0;

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Avalonia requires the {PropertyName}Property field convention.")]
    public static readonly StyledProperty<ICommand?> EditPersonalScheduleCommandProperty = AvaloniaProperty.Register<ScheduleBoardView, ICommand?>(nameof(EditPersonalScheduleCommand));

    private readonly Grid mBoardGrid;

    private readonly Border mBoardExportSurface;

    private readonly Border mBoardContextHeader;

    private readonly Border mBoardStickyHeaderContainer;

    private readonly Grid mBoardStickyDayHeaderGrid;

    private readonly ScrollViewer mScheduleScrollViewer;

    private bool mIsPngExport;

    private ScheduleBoardLayout mRenderedLayout;

    internal ScheduleBoardLayout RenderedLayout
    {
        get
        {
            return mRenderedLayout;
        }
    }

    internal Control PngExportSurface
    {
        get
        {
            return mBoardExportSurface;
        }
    }

    public ICommand? EditPersonalScheduleCommand
    {
        get
        {
            return GetValue(EditPersonalScheduleCommandProperty);
        }
        set
        {
            SetValue(EditPersonalScheduleCommandProperty, value);
        }
    }

    public ScheduleBoardView()
    {
        AvaloniaXamlLoader.Load(this);
        Border? boardExportSurfaceOrNull = this.FindControl<Border>("BoardExportSurface");
        Border? boardContextHeaderOrNull = this.FindControl<Border>("BoardContextHeader");
        Border? boardStickyHeaderContainerOrNull = this.FindControl<Border>("BoardStickyHeaderContainer");
        Grid? boardStickyDayHeaderGridOrNull = this.FindControl<Grid>("BoardStickyDayHeaderGrid");
        ScrollViewer? scheduleScrollViewerOrNull = this.FindControl<ScrollViewer>("ScheduleScrollViewer");
        Grid? boardGridOrNull = this.FindControl<Grid>("BoardGrid");
        if (boardExportSurfaceOrNull == null
            || boardContextHeaderOrNull == null
            || boardStickyHeaderContainerOrNull == null
            || boardStickyDayHeaderGridOrNull == null
            || scheduleScrollViewerOrNull == null
            || boardGridOrNull == null)
        {
            throw new InvalidOperationException("The schedule board surfaces were not initialized.");
        }

        mBoardExportSurface = boardExportSurfaceOrNull;
        mBoardContextHeader = boardContextHeaderOrNull;
        mBoardContextHeader.IsVisible = false;
        mBoardStickyHeaderContainer = boardStickyHeaderContainerOrNull;
        mBoardStickyDayHeaderGrid = boardStickyDayHeaderGridOrNull;
        mScheduleScrollViewer = scheduleScrollViewerOrNull;
        mBoardGrid = boardGridOrNull;
        mRenderedLayout = ScheduleBoardLayout.Default;
        DataContextChanged += onDataContextChanged;
        AttachedToVisualTree += onAttachedToVisualTree;
        ActualThemeVariantChanged += onActualThemeVariantChanged;
    }

    internal static ScheduleBoardView createForPngExport()
    {
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.mIsPngExport = true;
        scheduleBoard.mBoardContextHeader.IsVisible = true;
        scheduleBoard.mBoardStickyHeaderContainer.IsVisible = false;
        scheduleBoard.mBoardExportSurface.BorderThickness = new Thickness(1.0, 1.0, 1.0, 0.0);
        return scheduleBoard;
    }

    internal void prepareForPngExport(ScheduleBoardPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (mIsPngExport == false)
        {
            throw new InvalidOperationException("Only a PNG export board can be prepared for export.");
        }

        DataContext = presentation;
        rebuildBoard();
    }

    private void onDataContextChanged(object? senderOrNull, EventArgs eventArgs)
    {
        mScheduleScrollViewer.Offset = default;
        if (VisualRoot == null)
        {
            return;
        }

        rebuildBoard();
    }

    private void onAttachedToVisualTree(object? senderOrNull, VisualTreeAttachmentEventArgs eventArgs)
    {
        rebuildBoard();
    }

    private void onActualThemeVariantChanged(object? senderOrNull, EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(rebuildBoardAfterThemeChange, DispatcherPriority.Render);
    }

    private void rebuildBoardAfterThemeChange()
    {
        object? borderBrushOrNull;
        bool hasBorderBrush = ResourceNodeExtensions.TryFindResource(
            this,
            "BorderBrush",
            ActualThemeVariant,
            out borderBrushOrNull);
        if (VisualRoot == null || hasBorderBrush == false || borderBrushOrNull is not IBrush)
        {
            return;
        }

        rebuildBoard();
    }

    private void rebuildBoard()
    {
        ScheduleBoardPresentation? presentationOrNull = DataContext as ScheduleBoardPresentation;
        ScheduleRecommendation? recommendationOrNull = null;
        if (presentationOrNull == null)
        {
            mRenderedLayout = ScheduleBoardLayout.Default;
        }
        else
        {
            mRenderedLayout = presentationOrNull.Layout;
            recommendationOrNull = presentationOrNull.Schedule;
        }

        mBoardGrid.Children.Clear();
        mBoardGrid.ColumnDefinitions.Clear();
        mBoardGrid.RowDefinitions.Clear();
        mBoardStickyDayHeaderGrid.Children.Clear();
        mBoardStickyDayHeaderGrid.ColumnDefinitions.Clear();
        mBoardGrid.MinHeight = findBoardGridHeaderRowHeight()
            + (mRenderedLayout.TimeAxis.IncrementCount
                * TIME_INCREMENT_ROW_HEIGHT);

        addGridDefinitions();
        addGridGuides();
        addDayHeaders();
        addTimeHeaders();

        bool hasScheduleEntries = recommendationOrNull != null
            && recommendationOrNull.Entries.Count > 0;
        if (hasScheduleEntries == false || recommendationOrNull == null)
        {
            return;
        }

        addScheduleEndBoundary();
        foreach (ScheduleEntry entry in recommendationOrNull.Entries)
        {
            addScheduleEntry(entry);
        }
    }

    private void addGridDefinitions()
    {
        ColumnDefinition timeColumn = new ColumnDefinition();
        timeColumn.Width = new GridLength(TIME_COLUMN_WIDTH, GridUnitType.Pixel);
        mBoardGrid.ColumnDefinitions.Add(timeColumn);

        for (int dayIndex = 0; dayIndex < mRenderedLayout.DayRange.DayCount; ++dayIndex)
        {
            ColumnDefinition dayColumn = new ColumnDefinition();
            dayColumn.Width = new GridLength(1.0, GridUnitType.Star);
            mBoardGrid.ColumnDefinitions.Add(dayColumn);
        }

        RowDefinition headerRow = new RowDefinition();
        headerRow.Height = new GridLength(findBoardGridHeaderRowHeight(), GridUnitType.Pixel);
        mBoardGrid.RowDefinitions.Add(headerRow);

        for (int incrementIndex = 0; incrementIndex < mRenderedLayout.TimeAxis.IncrementCount; ++incrementIndex)
        {
            RowDefinition timeRow = new RowDefinition();
            timeRow.Height = new GridLength(TIME_INCREMENT_ROW_HEIGHT, GridUnitType.Pixel);
            mBoardGrid.RowDefinitions.Add(timeRow);
        }
    }

    private void addGridGuides()
    {
        int totalColumnCount = mRenderedLayout.DayRange.TotalColumnCount;
        for (int columnIndex = 0; columnIndex < totalColumnCount; ++columnIndex)
        {
            Border columnGuide = new Border();
            columnGuide.BorderBrush = findBrush("BorderBrush");
            columnGuide.BorderThickness = new Thickness(
                0.0,
                0.0,
                columnIndex < totalColumnCount - 1 ? 1.0 : 0.0,
                0.0);
            Grid.SetRow(columnGuide, 0);
            Grid.SetRowSpan(columnGuide, mRenderedLayout.TimeAxis.IncrementCount + 1);
            Grid.SetColumn(columnGuide, columnIndex);
            mBoardGrid.Children.Add(columnGuide);
        }

        Border headerGuide = new Border();
        headerGuide.BorderBrush = findBrush("BorderBrush");
        headerGuide.BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0);
        Grid.SetRow(headerGuide, 0);
        Grid.SetColumnSpan(headerGuide, totalColumnCount);
        mBoardGrid.Children.Add(headerGuide);

        foreach (ScheduleBoardTimeBoundary guideTime
            in mRenderedLayout.TimeAxis.GuideTimes)
        {
            int rowIndex = 1 + mRenderedLayout.TimeAxis.FindBoundaryRowOffset(guideTime);
            Border timeGuide = new Border();
            string gridLineBrushKey = guideTime.IsFullHour
                ? "ScheduleHourGridLineBrush"
                : "ScheduleHalfHourGridLineBrush";
            timeGuide.BorderBrush = findBrush(gridLineBrushKey);
            timeGuide.BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0);
            timeGuide.IsHitTestVisible = false;
            if (guideTime.IsFullHour)
            {
                timeGuide.Classes.Add("schedule-hour-guide");
                timeGuide.Margin = new Thickness(TIME_COLUMN_WIDTH - HOUR_GUIDE_EXTENSION_WIDTH, 0.0, 0.0, 0.0);
                Grid.SetColumn(timeGuide, 0);
                Grid.SetColumnSpan(timeGuide, totalColumnCount);
            }
            else
            {
                timeGuide.Classes.Add("schedule-half-hour-guide");
                Grid.SetColumn(timeGuide, 1);
                Grid.SetColumnSpan(timeGuide, totalColumnCount - 1);
            }

            Grid.SetRow(timeGuide, rowIndex);
            mBoardGrid.Children.Add(timeGuide);
        }
    }

    private void addScheduleEndBoundary()
    {
        Border endBoundary = createBottomBoundary("schedule-end-boundary");
        Grid.SetRow(endBoundary, mRenderedLayout.TimeAxis.IncrementCount);
        Grid.SetColumn(endBoundary, 1);
        Grid.SetColumnSpan(endBoundary, mRenderedLayout.DayRange.TotalColumnCount - 1);
        mBoardGrid.Children.Add(endBoundary);
    }

    private Border createBottomBoundary(string className)
    {
        Border boundary = new Border();
        boundary.Classes.Add(className);
        boundary.BorderBrush = findBrush("StrongBorderBrush");
        boundary.BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0);
        boundary.IsHitTestVisible = false;
        boundary.ZIndex = 3;
        AutomationProperties.SetAccessibilityView(boundary, AccessibilityView.Raw);
        return boundary;
    }

    private void addDayHeaders()
    {
        if (mIsPngExport == false)
        {
            addStickyDayHeaders();
            return;
        }

        foreach (ScheduleBoardDay day in mRenderedLayout.DayRange.Days)
        {
            TextBlock dayHeader = new TextBlock();
            dayHeader.Text = day.ShortDisplayName;
            dayHeader.FontSize = 14.0;
            dayHeader.FontWeight = FontWeight.SemiBold;
            dayHeader.HorizontalAlignment = HorizontalAlignment.Center;
            dayHeader.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(dayHeader, 0);
            Grid.SetColumn(dayHeader, day.ColumnIndex);
            mBoardGrid.Children.Add(dayHeader);
        }
    }

    private void addStickyDayHeaders()
    {
        ColumnDefinition timeColumn = new ColumnDefinition();
        timeColumn.Width = new GridLength(TIME_COLUMN_WIDTH, GridUnitType.Pixel);
        mBoardStickyDayHeaderGrid.ColumnDefinitions.Add(timeColumn);

        for (int dayIndex = 0; dayIndex < mRenderedLayout.DayRange.DayCount; ++dayIndex)
        {
            ColumnDefinition dayColumn = new ColumnDefinition();
            dayColumn.Width = new GridLength(1.0, GridUnitType.Star);
            mBoardStickyDayHeaderGrid.ColumnDefinitions.Add(dayColumn);
        }

        int totalColumnCount = mRenderedLayout.DayRange.TotalColumnCount;
        for (int columnIndex = 0; columnIndex < totalColumnCount; ++columnIndex)
        {
            Border columnGuide = new Border();
            columnGuide.BorderBrush = findBrush("BorderBrush");
            columnGuide.BorderThickness = new Thickness(
                0.0,
                0.0,
                columnIndex < totalColumnCount - 1 ? 1.0 : 0.0,
                0.0);
            Grid.SetColumn(columnGuide, columnIndex);
            mBoardStickyDayHeaderGrid.Children.Add(columnGuide);
        }

        foreach (ScheduleBoardDay day in mRenderedLayout.DayRange.Days)
        {
            TextBlock dayHeader = new TextBlock();
            dayHeader.Text = day.ShortDisplayName;
            dayHeader.FontSize = 14.0;
            dayHeader.FontWeight = FontWeight.SemiBold;
            dayHeader.HorizontalAlignment = HorizontalAlignment.Center;
            dayHeader.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(dayHeader, day.ColumnIndex);
            mBoardStickyDayHeaderGrid.Children.Add(dayHeader);
        }
    }

    private void addTimeHeaders()
    {
        foreach (ScheduleBoardTimeBoundary labelTime
            in mRenderedLayout.TimeAxis.LabelTimes)
        {
            TextBlock timeHeader = new TextBlock();
            timeHeader.Classes.Add("schedule-time-label");
            timeHeader.Text = labelTime.ToString();
            timeHeader.Width = TIME_LABEL_WIDTH;
            timeHeader.Height = TIME_LABEL_HEIGHT;
            timeHeader.FontSize = 11.0;
            timeHeader.FontWeight = FontWeight.SemiBold;
            timeHeader.LineHeight = TIME_LABEL_HEIGHT;
            timeHeader.Foreground = findBrush("TextSecondaryBrush");
            timeHeader.TextAlignment = TextAlignment.Right;
            timeHeader.HorizontalAlignment = HorizontalAlignment.Right;
            timeHeader.VerticalAlignment = VerticalAlignment.Center;
            timeHeader.Margin = new Thickness(0.0, 0.0, HOUR_GUIDE_EXTENSION_WIDTH + TIME_LABEL_GUIDE_GAP, 0.0);
            timeHeader.IsHitTestVisible = false;
            int boundaryRowIndex = 1 + mRenderedLayout.TimeAxis.FindBoundaryRowOffset(labelTime);
            int labelRowIndex = labelTime == mRenderedLayout.TimeAxis.Start
                ? boundaryRowIndex
                : boundaryRowIndex - 1;
            Grid.SetRow(timeHeader, labelRowIndex);
            Grid.SetRowSpan(timeHeader, 2);
            Grid.SetColumn(timeHeader, 0);
            mBoardGrid.Children.Add(timeHeader);
        }
    }

    private IBrush findBrush(string resourceKey)
    {
        object? resourceOrNull;
        bool hasResource = ResourceNodeExtensions.TryFindResource(
            this,
            resourceKey,
            ActualThemeVariant,
            out resourceOrNull);
        if (hasResource == false || resourceOrNull is not IBrush brush)
        {
            throw new InvalidOperationException("Missing brush resource: " + resourceKey);
        }

        return brush;
    }

    private double findBoardGridHeaderRowHeight()
    {
        return mIsPngExport ? HEADER_ROW_HEIGHT : 0.0;
    }
}
