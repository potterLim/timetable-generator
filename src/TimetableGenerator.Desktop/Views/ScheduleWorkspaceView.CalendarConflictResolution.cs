using System;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView
{
    public Task<ECalendarNameConflictResolution> ResolveAsync(
        CalendarNameConflict conflict,
        CancellationToken cancellationToken)
    {
        if (conflict == null)
        {
            throw new ArgumentNullException(nameof(conflict));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess())
        {
            return showCalendarNameConflictAsync(
                conflict,
                cancellationToken);
        }

        TaskCompletionSource<ECalendarNameConflictResolution> completionSource =
            new TaskCompletionSource<ECalendarNameConflictResolution>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(
            async delegate
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ECalendarNameConflictResolution resolution =
                        await showCalendarNameConflictAsync(
                            conflict,
                            cancellationToken);
                    completionSource.TrySetResult(resolution);
                }
                catch (OperationCanceledException)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                }
                catch (Exception exception)
                {
                    completionSource.TrySetException(exception);
                }
            });
        return completionSource.Task;
    }

    private async Task<ECalendarNameConflictResolution>
        showCalendarNameConflictAsync(
            CalendarNameConflict conflict,
            CancellationToken cancellationToken)
    {
        Window? ownerOrNull = TopLevel.GetTopLevel(this) as Window;
        if (ownerOrNull == null)
        {
            throw new InvalidOperationException(
                "Calendar export conflicts require an owner window.");
        }

        CalendarNameConflictDialog dialog =
            new CalendarNameConflictDialog(conflict);
        using (CancellationTokenRegistration registration =
            cancellationToken.Register(
                delegate
                {
                    Dispatcher.UIThread.Post(
                        delegate
                        {
                            if (dialog.IsVisible)
                            {
                                dialog.Close(
                                    ECalendarNameConflictResolution.Cancel);
                            }
                        });
                }))
        {
            ECalendarNameConflictResolution resolution =
                await dialog.ShowDialog<ECalendarNameConflictResolution>(
                    ownerOrNull);
            cancellationToken.ThrowIfCancellationRequested();
            if (resolution == ECalendarNameConflictResolution.None)
            {
                return ECalendarNameConflictResolution.Cancel;
            }

            CalendarNameConflictPolicy.EnsureResolutionIsSupported(
                conflict,
                resolution);
            return resolution;
        }
    }
}
