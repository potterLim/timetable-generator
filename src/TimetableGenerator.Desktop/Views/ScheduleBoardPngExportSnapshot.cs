using System;

using Avalonia;
using Avalonia.Controls;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed class ScheduleBoardPngExportSnapshot : IDisposable
{
    private const double MINIMUM_DAY_COLUMN_WIDTH = 132.0;
    private const double NON_DAY_CONTENT_WIDTH = 96.0;

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

    private ScheduleBoardPngExportSnapshot(
        Canvas host,
        ScheduleBoardView scheduleBoard)
    {
        mHost = host;
        mScheduleBoard = scheduleBoard;
        mIsDisposed = false;
    }

    public static ScheduleBoardPngExportSnapshot Create(
        Canvas host,
        ScheduleBoardView sourceBoard)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(sourceBoard);

        ScheduleBoardPresentation? sourcePresentationOrNull =
            sourceBoard.DataContext as ScheduleBoardPresentation;
        if (sourcePresentationOrNull == null)
        {
            throw new InvalidOperationException(
                "PNG export requires a rendered schedule presentation.");
        }

        ScheduleBoardLayout exportLayout = ScheduleBoardLayout.CreateForPngExport(
            sourcePresentationOrNull.Schedule.Entries);
        ScheduleBoardPresentation exportPresentation =
            new ScheduleBoardPresentation(
                sourcePresentationOrNull.Schedule,
                exportLayout,
                sourcePresentationOrNull.PlanName,
                sourcePresentationOrNull.InstitutionName,
                sourcePresentationOrNull.AcademicTerm);

        double minimumWidth = NON_DAY_CONTENT_WIDTH
            + (exportLayout.DayRange.DayCount * MINIMUM_DAY_COLUMN_WIDTH);
        double exportWidth = Math.Max(sourceBoard.Bounds.Width, minimumWidth);
        if (double.IsFinite(exportWidth) == false || exportWidth <= 0.0)
        {
            throw new InvalidOperationException(
                "PNG export requires a positive schedule board width.");
        }

        ScheduleBoardView exportBoard = ScheduleBoardView.createForPngExport();
        exportBoard.Width = exportWidth;
        host.Children.Add(exportBoard);
        exportBoard.DataContext = exportPresentation;
        exportBoard.Measure(new Size(exportWidth, double.PositiveInfinity));

        double exportHeight = exportBoard.DesiredSize.Height;
        if (double.IsFinite(exportHeight) == false || exportHeight <= 0.0)
        {
            host.Children.Remove(exportBoard);
            throw new InvalidOperationException(
                "PNG export could not measure the schedule board.");
        }

        exportBoard.Arrange(new Rect(0.0, 0.0, exportWidth, exportHeight));
        return new ScheduleBoardPngExportSnapshot(host, exportBoard);
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
}
