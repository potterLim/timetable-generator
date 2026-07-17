using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView : UserControl
{
    private const int DAY_COUNT = 5;
    private const int MINIMUM_VISIBLE_PERIOD_COUNT = 6;
    private const int TIME_INCREMENT_MINUTES = 5;
    private const int HALF_HOUR_MINUTES = 30;
    private const int HOUR_MINUTES = 60;
    private const int DEFAULT_AXIS_START_MINUTE = 510;
    private const int DEFAULT_AXIS_END_MINUTE = 1050;
    private const int MINUTES_PER_DAY = 1440;
    private const double PERIOD_COLUMN_WIDTH = 72.0;
    private const double HEADER_ROW_HEIGHT = 42.0;
    private const double TIME_INCREMENT_ROW_HEIGHT = 8.0;

    private static readonly string[] DAY_NAMES =
    {
        "월",
        "화",
        "수",
        "목",
        "금",
    };

    private readonly Grid mBoardGrid;

    private readonly StackPanel mBoardExportSurface;

    private readonly Border mPersonalScheduleLegendSurface;

    private readonly StackPanel mPersonalScheduleLegendEntries;

    private int mAxisStartMinute;

    private int mAxisEndMinute;

    internal int RenderedPeriodCount { get; private set; }

    internal Control PngExportSurface
    {
        get
        {
            return mBoardExportSurface;
        }
    }

    private int AxisIncrementCount
    {
        get
        {
            return (mAxisEndMinute - mAxisStartMinute) / TIME_INCREMENT_MINUTES;
        }
    }

    public ScheduleBoardView()
    {
        AvaloniaXamlLoader.Load(this);
        StackPanel? boardExportSurfaceOrNull = this.FindControl<StackPanel>(
            "BoardExportSurface");
        Grid? boardGridOrNull = this.FindControl<Grid>("BoardGrid");
        Border? personalScheduleLegendSurfaceOrNull = this.FindControl<Border>(
            "PersonalScheduleLegendSurface");
        StackPanel? personalScheduleLegendEntriesOrNull =
            this.FindControl<StackPanel>("PersonalScheduleLegendEntries");
        if (boardExportSurfaceOrNull == null
            || boardGridOrNull == null
            || personalScheduleLegendSurfaceOrNull == null
            || personalScheduleLegendEntriesOrNull == null)
        {
            throw new InvalidOperationException(
                "The schedule board export surface was not initialized.");
        }

        mBoardExportSurface = boardExportSurfaceOrNull;
        mBoardGrid = boardGridOrNull;
        mPersonalScheduleLegendSurface = personalScheduleLegendSurfaceOrNull;
        mPersonalScheduleLegendEntries = personalScheduleLegendEntriesOrNull;
        mAxisStartMinute = DEFAULT_AXIS_START_MINUTE;
        mAxisEndMinute = DEFAULT_AXIS_END_MINUTE;
        RenderedPeriodCount = MINIMUM_VISIBLE_PERIOD_COUNT;
        DataContextChanged += onDataContextChanged;
        AttachedToVisualTree += onAttachedToVisualTree;
        ActualThemeVariantChanged += onActualThemeVariantChanged;
    }

    private void onDataContextChanged(object? senderOrNull, EventArgs eventArgs)
    {
        if (VisualRoot == null)
        {
            return;
        }

        rebuildBoard();
    }

    private void onAttachedToVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        rebuildBoard();
    }

    private void onActualThemeVariantChanged(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(
            rebuildBoardAfterThemeChange,
            DispatcherPriority.Render);
    }

    private void rebuildBoardAfterThemeChange()
    {
        object? borderBrushOrNull;
        bool hasBorderBrush = ResourceNodeExtensions.TryFindResource(
            this,
            "BorderBrush",
            ActualThemeVariant,
            out borderBrushOrNull);
        if (VisualRoot == null
            || hasBorderBrush == false
            || borderBrushOrNull is not IBrush)
        {
            return;
        }

        rebuildBoard();
    }

    private void rebuildBoard()
    {
        ScheduleBoardPresentation? presentationOrNull =
            DataContext as ScheduleBoardPresentation;
        ScheduleRecommendation? recommendationOrNull = presentationOrNull == null
            ? null
            : presentationOrNull.Schedule;
        findTimeAxis(recommendationOrNull);
        RenderedPeriodCount = findRenderedPeriodCount(recommendationOrNull);

        mBoardGrid.Children.Clear();
        mBoardGrid.ColumnDefinitions.Clear();
        mBoardGrid.RowDefinitions.Clear();
        mBoardGrid.MinHeight = HEADER_ROW_HEIGHT
            + (AxisIncrementCount * TIME_INCREMENT_ROW_HEIGHT);

        addGridDefinitions();
        addGridGuides();
        addDayHeaders();
        addPeriodHeaders();
        addOutOfPeriodTimeHeaders();

        if (recommendationOrNull == null)
        {
            rebuildPersonalScheduleLegend(null);
            return;
        }

        foreach (ScheduleEntry entry in recommendationOrNull.Entries)
        {
            addScheduleEntry(entry);
        }

        rebuildPersonalScheduleLegend(recommendationOrNull);
    }

    private void addGridDefinitions()
    {
        ColumnDefinition periodColumn = new ColumnDefinition();
        periodColumn.Width = new GridLength(PERIOD_COLUMN_WIDTH, GridUnitType.Pixel);
        mBoardGrid.ColumnDefinitions.Add(periodColumn);

        for (int dayIndex = 0; dayIndex < DAY_COUNT; ++dayIndex)
        {
            ColumnDefinition dayColumn = new ColumnDefinition();
            dayColumn.Width = new GridLength(1.0, GridUnitType.Star);
            mBoardGrid.ColumnDefinitions.Add(dayColumn);
        }

        RowDefinition headerRow = new RowDefinition();
        headerRow.Height = new GridLength(HEADER_ROW_HEIGHT, GridUnitType.Pixel);
        mBoardGrid.RowDefinitions.Add(headerRow);

        for (int incrementIndex = 0;
            incrementIndex < AxisIncrementCount;
            ++incrementIndex)
        {
            RowDefinition timeRow = new RowDefinition();
            timeRow.Height = new GridLength(
                TIME_INCREMENT_ROW_HEIGHT,
                GridUnitType.Pixel);
            mBoardGrid.RowDefinitions.Add(timeRow);
        }
    }

    private void addGridGuides()
    {
        for (int columnIndex = 0; columnIndex <= DAY_COUNT; ++columnIndex)
        {
            Border columnGuide = new Border();
            columnGuide.BorderBrush = findBrush("BorderBrush");
            columnGuide.BorderThickness = new Thickness(
                0.0,
                0.0,
                columnIndex < DAY_COUNT ? 1.0 : 0.0,
                0.0);
            Grid.SetRow(columnGuide, 0);
            Grid.SetRowSpan(columnGuide, AxisIncrementCount + 1);
            Grid.SetColumn(columnGuide, columnIndex);
            mBoardGrid.Children.Add(columnGuide);
        }

        Border headerGuide = new Border();
        headerGuide.BorderBrush = findBrush("BorderBrush");
        headerGuide.BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0);
        Grid.SetRow(headerGuide, 0);
        Grid.SetColumnSpan(headerGuide, DAY_COUNT + 1);
        mBoardGrid.Children.Add(headerGuide);

        int firstGuideMinute = roundUp(mAxisStartMinute, HALF_HOUR_MINUTES);
        for (int guideMinute = firstGuideMinute;
            guideMinute < mAxisEndMinute;
            guideMinute += HALF_HOUR_MINUTES)
        {
            int rowIndex = 1 + getRowOffset(guideMinute);
            Border timeGuide = new Border();
            timeGuide.BorderBrush = findBrush("BorderBrush");
            timeGuide.BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0);
            timeGuide.Opacity = guideMinute % 60 == 0 ? 0.72 : 0.38;
            Grid.SetRow(timeGuide, rowIndex);
            Grid.SetColumnSpan(timeGuide, DAY_COUNT + 1);
            mBoardGrid.Children.Add(timeGuide);
        }
    }

    private void addDayHeaders()
    {
        for (int dayIndex = 0; dayIndex < DAY_COUNT; ++dayIndex)
        {
            TextBlock dayHeader = new TextBlock();
            dayHeader.Text = DAY_NAMES[dayIndex];
            dayHeader.FontSize = 14.0;
            dayHeader.FontWeight = FontWeight.SemiBold;
            dayHeader.HorizontalAlignment = HorizontalAlignment.Center;
            dayHeader.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(dayHeader, 0);
            Grid.SetColumn(dayHeader, dayIndex + 1);
            mBoardGrid.Children.Add(dayHeader);
        }
    }

    private void addPeriodHeaders()
    {
        for (int periodValue = AcademicPeriod.MINIMUM_VALUE;
            periodValue <= AcademicPeriod.MAXIMUM_VALUE;
            ++periodValue)
        {
            AcademicPeriod period = new AcademicPeriod(periodValue);
            DailyTimeRange timeRange = AcademicPeriodTimeTable.GetTimeRange(period);
            int startMinute = timeRange.Start.MinutesFromMidnight;
            int endMinute = timeRange.End.MinutesFromMidnight;
            if (startMinute < mAxisStartMinute || startMinute >= mAxisEndMinute)
            {
                continue;
            }

            int visibleEndMinute = Math.Min(endMinute, mAxisEndMinute);
            TextBlock periodHeader = new TextBlock();
            periodHeader.Text = periodValue + "교시\n" + timeRange.Start;
            periodHeader.FontSize = 11.0;
            periodHeader.FontWeight = FontWeight.SemiBold;
            periodHeader.Foreground = findBrush("TextSecondaryBrush");
            periodHeader.TextAlignment = TextAlignment.Center;
            periodHeader.HorizontalAlignment = HorizontalAlignment.Center;
            periodHeader.VerticalAlignment = VerticalAlignment.Top;
            periodHeader.Margin = new Thickness(0.0, 7.0, 0.0, 0.0);
            Grid.SetRow(periodHeader, 1 + getRowOffset(startMinute));
            Grid.SetRowSpan(
                periodHeader,
                Math.Max(
                    1,
                    getRowOffsetCeiling(visibleEndMinute)
                    - getRowOffset(startMinute)));
            Grid.SetColumn(periodHeader, 0);
            mBoardGrid.Children.Add(periodHeader);
        }
    }

    private void addOutOfPeriodTimeHeaders()
    {
        DailyTimeRange firstPeriod = AcademicPeriodTimeTable.GetTimeRange(
            new AcademicPeriod(AcademicPeriod.MINIMUM_VALUE));
        DailyTimeRange lastPeriod = AcademicPeriodTimeTable.GetTimeRange(
            new AcademicPeriod(AcademicPeriod.MAXIMUM_VALUE));
        int earlyLabelEndMinute = Math.Min(
            mAxisEndMinute,
            firstPeriod.Start.MinutesFromMidnight);
        for (int labelMinute = mAxisStartMinute;
            labelMinute < earlyLabelEndMinute;
            labelMinute += HOUR_MINUTES)
        {
            addOutOfPeriodTimeHeader(labelMinute);
        }

        int lateLabelStartMinute = Math.Max(
            mAxisStartMinute,
            roundUp(
                lastPeriod.End.MinutesFromMidnight,
                HALF_HOUR_MINUTES));
        for (int labelMinute = lateLabelStartMinute;
            labelMinute < mAxisEndMinute;
            labelMinute += HOUR_MINUTES)
        {
            addOutOfPeriodTimeHeader(labelMinute);
        }
    }

    private void addOutOfPeriodTimeHeader(int minuteOfDay)
    {
        ScheduleTime time = new ScheduleTime(
            minuteOfDay / HOUR_MINUTES,
            minuteOfDay % HOUR_MINUTES);
        TextBlock timeHeader = new TextBlock();
        timeHeader.Text = time.ToString();
        timeHeader.FontSize = 10.5;
        timeHeader.Foreground = findBrush("TextSecondaryBrush");
        timeHeader.TextAlignment = TextAlignment.Center;
        timeHeader.HorizontalAlignment = HorizontalAlignment.Center;
        timeHeader.VerticalAlignment = VerticalAlignment.Top;
        timeHeader.Margin = new Thickness(0.0, 5.0, 0.0, 0.0);
        Grid.SetRow(timeHeader, 1 + getRowOffset(minuteOfDay));
        Grid.SetRowSpan(timeHeader, HOUR_MINUTES / TIME_INCREMENT_MINUTES);
        Grid.SetColumn(timeHeader, 0);
        mBoardGrid.Children.Add(timeHeader);
    }

    private void findTimeAxis(ScheduleRecommendation? recommendationOrNull)
    {
        int earliestMinute = DEFAULT_AXIS_START_MINUTE;
        int latestMinute = DEFAULT_AXIS_END_MINUTE;
        if (recommendationOrNull != null)
        {
            foreach (ScheduleEntry entry in recommendationOrNull.Entries)
            {
                earliestMinute = Math.Min(
                    earliestMinute,
                    entry.TimeRange.Start.MinutesFromMidnight);
                latestMinute = Math.Max(
                    latestMinute,
                    entry.TimeRange.End.MinutesFromMidnight);
            }
        }

        mAxisStartMinute = roundDown(earliestMinute, HALF_HOUR_MINUTES);
        mAxisEndMinute = roundUp(latestMinute, HALF_HOUR_MINUTES);
        mAxisStartMinute = Math.Max(0, mAxisStartMinute);
        mAxisEndMinute = Math.Min(MINUTES_PER_DAY, mAxisEndMinute);
    }

    private static int findRenderedPeriodCount(
        ScheduleRecommendation? recommendationOrNull)
    {
        int renderedPeriodCount = MINIMUM_VISIBLE_PERIOD_COUNT;
        if (recommendationOrNull == null)
        {
            return renderedPeriodCount;
        }

        foreach (ScheduleEntry entry in recommendationOrNull.Entries)
        {
            CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
            if (courseEntryOrNull != null
                && courseEntryOrNull.Period.Value > renderedPeriodCount)
            {
                renderedPeriodCount = courseEntryOrNull.Period.Value;
            }
        }

        return renderedPeriodCount;
    }

    private int getRowOffset(int minuteOfDay)
    {
        return (minuteOfDay - mAxisStartMinute) / TIME_INCREMENT_MINUTES;
    }

    private int getRowOffsetCeiling(int minuteOfDay)
    {
        int minuteOffset = minuteOfDay - mAxisStartMinute;
        return (minuteOffset + TIME_INCREMENT_MINUTES - 1) / TIME_INCREMENT_MINUTES;
    }

    private static int roundDown(int value, int increment)
    {
        return (value / increment) * increment;
    }

    private static int roundUp(int value, int increment)
    {
        return ((value + increment - 1) / increment) * increment;
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
}
