using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleBoardView : UserControl
{
    private const int DAY_COUNT = 5;
    private const int MINIMUM_VISIBLE_PERIOD_COUNT = 6;
    private const double PERIOD_COLUMN_WIDTH = 80.0;
    private const double HEADER_ROW_HEIGHT = 42.0;
    private const double MINIMUM_PERIOD_ROW_HEIGHT = 96.0;

    private static readonly string[] DAY_NAMES =
    {
        "월",
        "화",
        "수",
        "목",
        "금",
    };

    private static readonly string[] PERIOD_TIME_RANGES =
    {
        "08:30–09:45",
        "10:00–11:15",
        "11:30–12:45",
        "13:00–14:15",
        "14:30–15:45",
        "16:00–17:15",
        "17:30–18:45",
        "19:00–20:15",
        "20:30–21:45",
        "22:00–23:15",
    };

    private readonly Grid mBoardGrid;

    internal int RenderedPeriodCount { get; private set; }

    internal Control PngExportSurface
    {
        get
        {
            return mBoardGrid;
        }
    }

    public ScheduleBoardView()
    {
        AvaloniaXamlLoader.Load(this);
        Grid? boardGridOrNull = this.FindControl<Grid>("BoardGrid");
        if (boardGridOrNull == null)
        {
            throw new InvalidOperationException("The schedule board grid was not initialized.");
        }

        mBoardGrid = boardGridOrNull;
        RenderedPeriodCount = MINIMUM_VISIBLE_PERIOD_COUNT;
        DataContextChanged += onDataContextChanged;
        AttachedToVisualTree += onAttachedToVisualTree;
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

    private void rebuildBoard()
    {
        ScheduleRecommendation? recommendationOrNull = DataContext as ScheduleRecommendation;
        RenderedPeriodCount = findRenderedPeriodCount(recommendationOrNull);

        mBoardGrid.Children.Clear();
        mBoardGrid.ColumnDefinitions.Clear();
        mBoardGrid.RowDefinitions.Clear();
        mBoardGrid.MinHeight = HEADER_ROW_HEIGHT
            + (RenderedPeriodCount * MINIMUM_PERIOD_ROW_HEIGHT);

        addGridDefinitions();
        addBackgroundCells();
        addDayHeaders();
        addPeriodHeaders();

        if (recommendationOrNull == null)
        {
            return;
        }

        foreach (ScheduleEntry entry in recommendationOrNull.Entries)
        {
            addScheduleEntry(entry);
        }
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

        for (int periodIndex = 0; periodIndex < RenderedPeriodCount; ++periodIndex)
        {
            RowDefinition periodRow = new RowDefinition();
            periodRow.Height = GridLength.Auto;
            periodRow.MinHeight = MINIMUM_PERIOD_ROW_HEIGHT;
            mBoardGrid.RowDefinitions.Add(periodRow);
        }
    }

    private void addBackgroundCells()
    {
        for (int rowIndex = 0; rowIndex <= RenderedPeriodCount; ++rowIndex)
        {
            for (int columnIndex = 0; columnIndex <= DAY_COUNT; ++columnIndex)
            {
                Border cellBorder = new Border();
                cellBorder.BorderBrush = findBrush("BorderBrush");
                cellBorder.BorderThickness = new Thickness(
                    0.0,
                    0.0,
                    columnIndex < DAY_COUNT ? 1.0 : 0.0,
                    rowIndex < RenderedPeriodCount ? 1.0 : 0.0);
                Grid.SetRow(cellBorder, rowIndex);
                Grid.SetColumn(cellBorder, columnIndex);
                mBoardGrid.Children.Add(cellBorder);
            }
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
        for (int periodIndex = 0; periodIndex < RenderedPeriodCount; ++periodIndex)
        {
            StackPanel periodHeader = new StackPanel();
            periodHeader.Spacing = 4.0;
            periodHeader.HorizontalAlignment = HorizontalAlignment.Center;
            periodHeader.VerticalAlignment = VerticalAlignment.Center;

            TextBlock periodName = new TextBlock();
            periodName.Text = (periodIndex + 1) + "교시";
            periodName.FontWeight = FontWeight.SemiBold;
            periodName.HorizontalAlignment = HorizontalAlignment.Center;
            periodHeader.Children.Add(periodName);

            TextBlock timeRange = new TextBlock();
            timeRange.Text = PERIOD_TIME_RANGES[periodIndex];
            timeRange.FontSize = 11.0;
            timeRange.Foreground = findBrush("TextSecondaryBrush");
            timeRange.HorizontalAlignment = HorizontalAlignment.Center;
            periodHeader.Children.Add(timeRange);

            Grid.SetRow(periodHeader, periodIndex + 1);
            Grid.SetColumn(periodHeader, 0);
            mBoardGrid.Children.Add(periodHeader);
        }
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
            if (entry.Period.Value > renderedPeriodCount)
            {
                renderedPeriodCount = entry.Period.Value;
            }
        }

        return renderedPeriodCount;
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
