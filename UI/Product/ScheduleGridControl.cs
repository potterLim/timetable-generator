using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleGridControl : DataGridView
{
    private const int AXIS_COLUMN_WIDTH = 96;
    private const int DAY_COLUMN_MINIMUM_WIDTH = 116;
    private const int COLUMN_HEADER_HEIGHT = 44;
    private const int PERIOD_ROW_HEIGHT = 88;
    private const int COURSE_CARD_INSET = 6;
    private const int COURSE_CARD_PADDING = 10;

    private readonly Font mAxisPeriodFont;
    private readonly Font mAxisTimeFont;
    private readonly Font mCourseTitleFont;
    private readonly Font mCourseDetailFont;
    private ScheduleGridViewModel? mViewModelOrNull;

    internal ScheduleGridControl()
    {
        mAxisPeriodFont = DesignTokens.createSectionTitleFont(Font);
        mAxisTimeFont = DesignTokens.createCaptionFont(Font);
        mCourseTitleFont = DesignTokens.createSidebarItemTitleFont(Font);
        mCourseDetailFont = DesignTokens.createCaptionFont(Font);

        AccessibleName = "시간표";
        AccessibleDescription = "요일별 강의와 교시 시간을 표시합니다.";
        AccessibleRole = AccessibleRole.Table;

        AutoGenerateColumns = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToOrderColumns = false;
        AllowUserToResizeRows = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        BackgroundColor = DesignTokens.SURFACE_COLOR;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.Single;
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        EnableHeadersVisualStyles = false;
        GridColor = DesignTokens.SUBTLE_BORDER_COLOR;
        MultiSelect = false;
        ReadOnly = true;
        RowHeadersVisible = false;
        ScrollBars = ScrollBars.Both;
        SelectionMode = DataGridViewSelectionMode.CellSelect;
        ShowCellErrors = false;
        ShowCellToolTips = true;
        ShowEditingIcon = false;
        StandardTab = false;
        TabStop = true;

        ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.SUBTLE_SURFACE_COLOR;
        ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        ColumnHeadersDefaultCellStyle.Font = mAxisPeriodFont;
        ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignTokens.SUBTLE_SURFACE_COLOR;
        ColumnHeadersDefaultCellStyle.SelectionForeColor = DesignTokens.TEXT_PRIMARY_COLOR;

        DefaultCellStyle.BackColor = DesignTokens.SURFACE_COLOR;
        DefaultCellStyle.ForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        DefaultCellStyle.SelectionBackColor = DesignTokens.ACCENT_TINT_COLOR;
        DefaultCellStyle.SelectionForeColor = DesignTokens.TEXT_PRIMARY_COLOR;
        DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        DoubleBuffered = true;
        CellPainting += onCellPainting;

        applyDpiMetrics();
    }

    internal void showSchedule(ScheduleGridViewModel viewModel)
    {
        if (viewModel == null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        mViewModelOrNull = viewModel;

        SuspendLayout();
        try
        {
            Rows.Clear();
            Columns.Clear();
            addColumns(viewModel);
            addRows(viewModel);
            applyDpiMetrics();
        }
        finally
        {
            ResumeLayout(true);
        }

        ClearSelection();
        if (Rows.Count > 0 && Columns.Count > 1)
        {
            CurrentCell = Rows[0].Cells[1];
        }

        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            mAxisPeriodFont.Dispose();
            mAxisTimeFont.Dispose();
            mCourseTitleFont.Dispose();
            mCourseDetailFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnDpiChangedAfterParent(EventArgs eventArgs)
    {
        base.OnDpiChangedAfterParent(eventArgs);
        applyDpiMetrics();
        Invalidate();
    }

    private void addColumns(ScheduleGridViewModel viewModel)
    {
        DataGridViewTextBoxColumn periodColumn = new DataGridViewTextBoxColumn();
        periodColumn.Name = "Period";
        periodColumn.HeaderText = "교시";
        periodColumn.ReadOnly = true;
        periodColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        periodColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        Columns.Add(periodColumn);

        foreach (ScheduleDayColumnViewModel dayColumnViewModel in viewModel.DayColumns)
        {
            DataGridViewTextBoxColumn dayColumn = new DataGridViewTextBoxColumn();
            dayColumn.Name = "Day" + dayColumnViewModel.Day;
            dayColumn.HeaderText = dayColumnViewModel.DisplayName;
            dayColumn.ReadOnly = true;
            dayColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            dayColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dayColumn.FillWeight = 100.0f;
            dayColumn.Tag = dayColumnViewModel;
            Columns.Add(dayColumn);
        }
    }

    private void addRows(ScheduleGridViewModel viewModel)
    {
        foreach (SchedulePeriodRowViewModel periodRowViewModel in viewModel.PeriodRows)
        {
            int rowIndex = Rows.Add();
            DataGridViewRow row = Rows[rowIndex];
            row.Tag = periodRowViewModel;

            DataGridViewCell periodCell = row.Cells[0];
            periodCell.Tag = periodRowViewModel;
            periodCell.Value = buildPeriodAccessibleText(periodRowViewModel);
            periodCell.ToolTipText = buildPeriodAccessibleText(periodRowViewModel);

            for (int cellIndex = 0; cellIndex < periodRowViewModel.Cells.Count; ++cellIndex)
            {
                ScheduleCellViewModel cellViewModel = periodRowViewModel.Cells[cellIndex];
                DataGridViewCell cell = row.Cells[cellIndex + 1];
                cell.Tag = cellViewModel;
                cell.Value = buildCourseAccessibleText(cellViewModel);
                cell.ToolTipText = buildCourseAccessibleText(cellViewModel);
            }
        }
    }

    private void applyDpiMetrics()
    {
        ColumnHeadersHeight = DesignTokens.scaleLogicalPixel(this, COLUMN_HEADER_HEIGHT);
        RowTemplate.Height = DesignTokens.scaleLogicalPixel(this, PERIOD_ROW_HEIGHT);

        if (Columns.Count > 0)
        {
            Columns[0].Width = DesignTokens.scaleLogicalPixel(this, AXIS_COLUMN_WIDTH);
            Columns[0].MinimumWidth = Columns[0].Width;

            int minimumDayColumnWidth = DesignTokens.scaleLogicalPixel(
                this,
                DAY_COLUMN_MINIMUM_WIDTH);
            for (int columnIndex = 1; columnIndex < Columns.Count; ++columnIndex)
            {
                Columns[columnIndex].MinimumWidth = minimumDayColumnWidth;
            }
        }

        int rowHeight = DesignTokens.scaleLogicalPixel(this, PERIOD_ROW_HEIGHT);
        foreach (DataGridViewRow row in Rows)
        {
            row.Height = rowHeight;
            row.MinimumHeight = rowHeight;
        }
    }

    private void onCellPainting(
        object? senderOrNull,
        DataGridViewCellPaintingEventArgs cellPaintingEventArgs)
    {
        Graphics? graphicsOrNull = cellPaintingEventArgs.Graphics;
        if (mViewModelOrNull == null ||
            graphicsOrNull == null ||
            cellPaintingEventArgs.RowIndex < 0 ||
            cellPaintingEventArgs.ColumnIndex < 0)
        {
            return;
        }

        Graphics graphics = graphicsOrNull;

        cellPaintingEventArgs.Handled = true;
        bool isSelected =
            (cellPaintingEventArgs.State & DataGridViewElementStates.Selected) ==
            DataGridViewElementStates.Selected;
        Color backgroundColor = isSelected
            ? DesignTokens.ACCENT_TINT_COLOR
            : DesignTokens.SURFACE_COLOR;

        if (cellPaintingEventArgs.ColumnIndex == 0)
        {
            backgroundColor = isSelected
                ? DesignTokens.ACCENT_TINT_COLOR
                : DesignTokens.SUBTLE_SURFACE_COLOR;
            drawCellBackground(graphics, cellPaintingEventArgs, backgroundColor);
            drawPeriodAxisCell(graphics, cellPaintingEventArgs);
        }
        else
        {
            drawCellBackground(graphics, cellPaintingEventArgs, backgroundColor);
            drawCourseCell(graphics, cellPaintingEventArgs);
        }

        drawCellBorder(graphics, cellPaintingEventArgs);
        drawCellFocus(graphics, cellPaintingEventArgs, backgroundColor);
    }

    private static void drawCellBackground(
        Graphics graphics,
        DataGridViewCellPaintingEventArgs cellPaintingEventArgs,
        Color backgroundColor)
    {
        using (SolidBrush backgroundBrush = new SolidBrush(backgroundColor))
        {
            graphics.FillRectangle(
                backgroundBrush,
                cellPaintingEventArgs.CellBounds);
        }
    }

    private void drawPeriodAxisCell(
        Graphics graphics,
        DataGridViewCellPaintingEventArgs cellPaintingEventArgs)
    {
        if (Rows[cellPaintingEventArgs.RowIndex].Cells[0].Tag is not
            SchedulePeriodRowViewModel periodRowViewModel)
        {
            return;
        }

        string periodText = periodRowViewModel.Period.Value + "교시";
        string timeText = formatTimeRange(periodRowViewModel.TimeRange);
        int contentGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4);
        Size periodTextSize = TextRenderer.MeasureText(
            graphics,
            periodText,
            mAxisPeriodFont,
            cellPaintingEventArgs.CellBounds.Size,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Size timeTextSize = TextRenderer.MeasureText(
            graphics,
            timeText,
            mAxisTimeFont,
            cellPaintingEventArgs.CellBounds.Size,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int contentHeight = periodTextSize.Height + contentGap + timeTextSize.Height;
        int contentTop = cellPaintingEventArgs.CellBounds.Top +
            ((cellPaintingEventArgs.CellBounds.Height - contentHeight) / 2);

        Rectangle periodBounds = new Rectangle(
            cellPaintingEventArgs.CellBounds.Left,
            contentTop,
            cellPaintingEventArgs.CellBounds.Width,
            periodTextSize.Height);
        Rectangle timeBounds = new Rectangle(
            cellPaintingEventArgs.CellBounds.Left,
            periodBounds.Bottom + contentGap,
            cellPaintingEventArgs.CellBounds.Width,
            timeTextSize.Height);

        TextRenderer.DrawText(
            graphics,
            periodText,
            mAxisPeriodFont,
            periodBounds,
            DesignTokens.TEXT_PRIMARY_COLOR,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(
            graphics,
            timeText,
            mAxisTimeFont,
            timeBounds,
            DesignTokens.TEXT_SECONDARY_COLOR,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private void drawCourseCell(
        Graphics graphics,
        DataGridViewCellPaintingEventArgs cellPaintingEventArgs)
    {
        if (Rows[cellPaintingEventArgs.RowIndex]
                .Cells[cellPaintingEventArgs.ColumnIndex].Tag is not
            ScheduleCellViewModel cellViewModel ||
            cellViewModel.HasCourseOffering == false)
        {
            return;
        }

        int cardInset = DesignTokens.scaleLogicalPixel(this, COURSE_CARD_INSET);
        Rectangle cardBounds = ProductDrawing.insetRectangle(
            cellPaintingEventArgs.CellBounds,
            cardInset);
        int cornerRadius = DesignTokens.scaleLogicalPixel(this, DesignTokens.CORNER_RADIUS_MEDIUM);
        Color backgroundColor = findCourseBackgroundColor(cellViewModel);
        Color borderColor = findCourseBorderColor(cellViewModel);
        Color textColor = findCourseTextColor(cellViewModel);

        GraphicsState graphicsState = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath cardPath =
                ProductDrawing.createRoundedRectanglePath(cardBounds, cornerRadius))
            {
                using (SolidBrush cardBrush = new SolidBrush(backgroundColor))
                {
                    using (Pen cardBorderPen = new Pen(
                        borderColor,
                        DesignTokens.scaleLogicalPixel(
                            this,
                            DesignTokens.BORDER_WIDTH)))
                    {
                        graphics.FillPath(cardBrush, cardPath);
                        graphics.DrawPath(cardBorderPen, cardPath);
                    }
                }
            }
        }
        finally
        {
            graphics.Restore(graphicsState);
        }

        int cardPadding = DesignTokens.scaleLogicalPixel(this, COURSE_CARD_PADDING);
        Rectangle contentBounds = ProductDrawing.insetRectangle(cardBounds, cardPadding);
        drawCourseCardText(
            graphics,
            contentBounds,
            cellViewModel,
            textColor);
    }

    private void drawCourseCardText(
        Graphics graphics,
        Rectangle contentBounds,
        ScheduleCellViewModel cellViewModel,
        Color textColor)
    {
        if (cellViewModel.HasClassroom == false)
        {
            TextRenderer.DrawText(
                graphics,
                cellViewModel.CourseDisplayName,
                mCourseTitleFont,
                contentBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            return;
        }

        string classroomText = cellViewModel.GetClassroomDisplayText();
        int contentGap = DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4);
        int classroomHeight = TextRenderer.MeasureText(
            graphics,
            classroomText,
            mCourseDetailFont,
            contentBounds.Size,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
        int titleHeight = Math.Max(0, contentBounds.Height - classroomHeight - contentGap);
        Rectangle titleBounds = new Rectangle(
            contentBounds.Left,
            contentBounds.Top,
            contentBounds.Width,
            titleHeight);
        Rectangle classroomBounds = new Rectangle(
            contentBounds.Left,
            titleBounds.Bottom + contentGap,
            contentBounds.Width,
            classroomHeight);

        TextRenderer.DrawText(
            graphics,
            cellViewModel.CourseDisplayName,
            mCourseTitleFont,
            titleBounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            graphics,
            classroomText,
            mCourseDetailFont,
            classroomBounds,
            DesignTokens.TEXT_SECONDARY_COLOR,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    private static void drawCellBorder(
        Graphics graphics,
        DataGridViewCellPaintingEventArgs cellPaintingEventArgs)
    {
        Rectangle bounds = cellPaintingEventArgs.CellBounds;
        using (Pen borderPen = new Pen(DesignTokens.SUBTLE_BORDER_COLOR))
        {
            graphics.DrawLine(
                borderPen,
                bounds.Left,
                bounds.Bottom - 1,
                bounds.Right - 1,
                bounds.Bottom - 1);
            graphics.DrawLine(
                borderPen,
                bounds.Right - 1,
                bounds.Top,
                bounds.Right - 1,
                bounds.Bottom - 1);
        }
    }

    private void drawCellFocus(
        Graphics graphics,
        DataGridViewCellPaintingEventArgs cellPaintingEventArgs,
        Color backgroundColor)
    {
        bool isCurrentCell =
            CurrentCellAddress.X == cellPaintingEventArgs.ColumnIndex &&
            CurrentCellAddress.Y == cellPaintingEventArgs.RowIndex;
        if (isCurrentCell == false || Focused == false || ShowFocusCues == false)
        {
            return;
        }

        Rectangle focusBounds = ProductDrawing.insetRectangle(
            cellPaintingEventArgs.CellBounds,
            DesignTokens.scaleLogicalPixel(this, DesignTokens.SPACE_4));
        ControlPaint.DrawFocusRectangle(
            graphics,
            focusBounds,
            DesignTokens.TEXT_PRIMARY_COLOR,
            backgroundColor);
    }

    private static string buildPeriodAccessibleText(SchedulePeriodRowViewModel periodRowViewModel)
    {
        return periodRowViewModel.Period.Value + "교시, " +
            formatTimeRange(periodRowViewModel.TimeRange);
    }

    private static string buildCourseAccessibleText(ScheduleCellViewModel cellViewModel)
    {
        if (cellViewModel.HasCourseOffering == false)
        {
            return "빈 시간";
        }

        if (cellViewModel.HasClassroom)
        {
            return cellViewModel.CourseDisplayName + ", " +
                cellViewModel.GetClassroomDisplayText();
        }

        return cellViewModel.CourseDisplayName;
    }

    private static string formatTimeRange(AcademicPeriodTimeRange timeRange)
    {
        return timeRange.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture) + "–" +
            timeRange.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static Color findCourseBackgroundColor(ScheduleCellViewModel cellViewModel)
    {
        EScheduleCourseColor courseColor = ScheduleCourseColorPolicy.findColor(
            cellViewModel.GetCourseOffering().ChoiceGroupId);
        switch (courseColor)
        {
            case EScheduleCourseColor.Blue:
                return DesignTokens.COURSE_BLUE_BACKGROUND_COLOR;
            case EScheduleCourseColor.Green:
                return DesignTokens.COURSE_GREEN_BACKGROUND_COLOR;
            case EScheduleCourseColor.Purple:
                return DesignTokens.COURSE_PURPLE_BACKGROUND_COLOR;
            default:
                Debug.Fail("Unexpected schedule course color: " + courseColor);
                throw new ArgumentOutOfRangeException(nameof(courseColor));
        }
    }

    private static Color findCourseBorderColor(ScheduleCellViewModel cellViewModel)
    {
        EScheduleCourseColor courseColor = ScheduleCourseColorPolicy.findColor(
            cellViewModel.GetCourseOffering().ChoiceGroupId);
        switch (courseColor)
        {
            case EScheduleCourseColor.Blue:
                return DesignTokens.COURSE_BLUE_BORDER_COLOR;
            case EScheduleCourseColor.Green:
                return DesignTokens.COURSE_GREEN_BORDER_COLOR;
            case EScheduleCourseColor.Purple:
                return DesignTokens.COURSE_PURPLE_BORDER_COLOR;
            default:
                Debug.Fail("Unexpected schedule course color: " + courseColor);
                throw new ArgumentOutOfRangeException(nameof(courseColor));
        }
    }

    private static Color findCourseTextColor(ScheduleCellViewModel cellViewModel)
    {
        EScheduleCourseColor courseColor = ScheduleCourseColorPolicy.findColor(
            cellViewModel.GetCourseOffering().ChoiceGroupId);
        switch (courseColor)
        {
            case EScheduleCourseColor.Blue:
                return DesignTokens.COURSE_BLUE_TEXT_COLOR;
            case EScheduleCourseColor.Green:
                return DesignTokens.COURSE_GREEN_TEXT_COLOR;
            case EScheduleCourseColor.Purple:
                return DesignTokens.COURSE_PURPLE_TEXT_COLOR;
            default:
                Debug.Fail("Unexpected schedule course color: " + courseColor);
                throw new ArgumentOutOfRangeException(nameof(courseColor));
        }
    }
}
