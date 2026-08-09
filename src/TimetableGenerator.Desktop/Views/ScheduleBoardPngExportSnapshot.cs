using System;

using Avalonia;
using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed class ScheduleBoardPngExportSnapshot : IDisposable
{
    private const double PRODUCT_DAY_COLUMN_WIDTH = 300.0;
    private const double NON_DAY_CONTENT_WIDTH = 96.0;
    private const double BOARD_FRAME_HORIZONTAL_BORDER_WIDTH = 2.0;

    private readonly Canvas mHost;

    private readonly ScheduleBoardView mScheduleBoard;

    private bool mIsDisposed;

    public Control Surface
    {
        get
        {
            return mScheduleBoard.PngExportSurface;
        }
    }

    internal ScheduleBoardLayout Layout
    {
        get
        {
            return mScheduleBoard.RenderedLayout;
        }
    }

    private ScheduleBoardPngExportSnapshot(Canvas host, ScheduleBoardView scheduleBoard)
    {
        mHost = host;
        mScheduleBoard = scheduleBoard;
        mIsDisposed = false;
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mHost.Children.Remove(mScheduleBoard);
        mIsDisposed = true;
    }

    internal static ScheduleBoardPngExportSnapshot create(Canvas host, ScheduleBoardPresentation sourcePresentation)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(sourcePresentation);

        ScheduleBoardView exportBoard = ScheduleBoardView.createForPngExport();
        host.Children.Add(exportBoard);
        ScheduleBoardPngExportSnapshot snapshot = new ScheduleBoardPngExportSnapshot(host, exportBoard);
        try
        {
            snapshot.update(sourcePresentation);
            return snapshot;
        }
        catch
        {
            snapshot.Dispose();
            throw;
        }
    }

    internal void update(ScheduleBoardPresentation sourcePresentation)
    {
        ArgumentNullException.ThrowIfNull(sourcePresentation);
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(ScheduleBoardPngExportSnapshot));
        }

        ScheduleBoardLayout exportLayout = ScheduleBoardLayout.CreateForPngExport(sourcePresentation.Schedule.Entries);
        ScheduleBoardPresentation exportPresentation = new ScheduleBoardPresentation(
            sourcePresentation.Schedule,
            exportLayout,
            sourcePresentation.PlanName,
            sourcePresentation.InstitutionName,
            sourcePresentation.AcademicTerm);

        double exportSurfaceWidth = NON_DAY_CONTENT_WIDTH + (exportLayout.DayRange.DayCount * PRODUCT_DAY_COLUMN_WIDTH);
        if (double.IsFinite(exportSurfaceWidth) == false || exportSurfaceWidth <= 0.0)
        {
            throw new InvalidOperationException("PNG export requires a positive schedule board width.");
        }

        double exportBoardWidth = exportSurfaceWidth + BOARD_FRAME_HORIZONTAL_BORDER_WIDTH;
        mScheduleBoard.Width = exportBoardWidth;
        mScheduleBoard.prepareForPngExport(exportPresentation);
        mScheduleBoard.Measure(new Size(exportBoardWidth, double.PositiveInfinity));

        double exportHeight = mScheduleBoard.DesiredSize.Height;
        if (double.IsFinite(exportHeight) == false || exportHeight <= 0.0)
        {
            throw new InvalidOperationException("PNG export could not measure the schedule board.");
        }

        mScheduleBoard.Arrange(new Rect(0.0, 0.0, exportBoardWidth, exportHeight));
        mScheduleBoard.UpdateLayout();
    }

}
