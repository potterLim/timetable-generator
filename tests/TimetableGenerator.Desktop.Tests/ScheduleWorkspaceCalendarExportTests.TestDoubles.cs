using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
    private sealed class RecordingGoogleCalendarWebNavigator : IGoogleCalendarWebNavigator
    {
        private readonly bool mOpenResult;

        public int OpenAttemptCount { get; private set; }

        public RecordingGoogleCalendarWebNavigator(bool openResult)
        {
            mOpenResult = openResult;
        }

        public bool TryOpen()
        {
            OpenAttemptCount++;
            return mOpenResult;
        }
    }

    private sealed class ControlledGoogleCalendarExporter : IGoogleCalendarExporter
    {
        private readonly TaskCompletionSource mExportStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<GoogleCalendarExportResult> mCompletionSource = new TaskCompletionSource<GoogleCalendarExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ExportStartedTask
        {
            get
            {
                return mExportStartedSource.Task;
            }
        }

        public async Task<GoogleCalendarExportResult> ExportAsync(GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            cancellationToken.ThrowIfCancellationRequested();
            mExportStartedSource.TrySetResult();
            return await mCompletionSource.Task.WaitAsync(cancellationToken);
        }

        public void Complete(GoogleCalendarExportResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (mCompletionSource.TrySetResult(result) == false)
            {
                throw new InvalidOperationException("The controlled export already completed.");
            }
        }

        public void CancelPendingExport()
        {
            mCompletionSource.TrySetCanceled();
        }

        public void Dispose()
        {
        }
    }

    private sealed class ControlledAppleCalendarExporter : IAppleCalendarExporter
    {
        private readonly TaskCompletionSource mExportStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<AppleCalendarExportResult> mCompletionSource = new TaskCompletionSource<AppleCalendarExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        private IProgress<AppleCalendarExportProgress>? mProgressOrNull;

        public bool IsAvailable
        {
            get
            {
                return true;
            }
        }

        public int ExportCallCount { get; private set; }

        public Task ExportStartedTask
        {
            get
            {
                return mExportStartedSource.Task;
            }
        }

        public async Task<AppleCalendarExportResult> ExportAsync(
            CalendarExportDocument document,
            ICalendarNameConflictResolver conflictResolver,
            CancellationToken cancellationToken,
            IProgress<AppleCalendarExportProgress>? progressOrNull = null)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            cancellationToken.ThrowIfCancellationRequested();
            ExportCallCount++;
            mProgressOrNull = progressOrNull;
            mExportStartedSource.TrySetResult();
            return await mCompletionSource.Task.WaitAsync(cancellationToken);
        }

        public void Report(EAppleCalendarExportProgressStage stage)
        {
            IProgress<AppleCalendarExportProgress>? progressOrNull = mProgressOrNull;
            if (progressOrNull == null)
            {
                throw new InvalidOperationException("The controlled export has no progress reporter.");
            }

            progressOrNull.Report(new AppleCalendarExportProgress(stage));
        }

        public void Complete(AppleCalendarExportResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (mCompletionSource.TrySetResult(result) == false)
            {
                throw new InvalidOperationException("The controlled export already completed.");
            }
        }

        public void CancelPendingExport()
        {
            mCompletionSource.TrySetCanceled();
        }
    }

    private sealed class QueueGoogleCalendarExporter : IGoogleCalendarExporter
    {
        private readonly Queue<GoogleCalendarExportResult> mResults;

        public QueueGoogleCalendarExporter(params GoogleCalendarExportResult[] results)
        {
            mResults = new Queue<GoogleCalendarExportResult>(results);
        }

        public Task<GoogleCalendarExportResult> ExportAsync(GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            cancellationToken.ThrowIfCancellationRequested();
            if (mResults.Count == 0)
            {
                throw new InvalidOperationException("No queued Google Calendar export result remains.");
            }

            return Task.FromResult(mResults.Dequeue());
        }

        public void Dispose()
        {
        }
    }
}
