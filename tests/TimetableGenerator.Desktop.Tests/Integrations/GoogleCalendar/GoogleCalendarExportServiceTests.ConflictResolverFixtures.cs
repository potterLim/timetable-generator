using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    private sealed class RecordingConflictResolver : ICalendarNameConflictResolver
    {
        private readonly ECalendarNameConflictResolution mResolution;

        private readonly List<CalendarNameConflict> mConflicts = new List<CalendarNameConflict>();

        public int CallCount { get; private set; }

        public CalendarNameConflict? ConflictOrNull { get; private set; }

        public IReadOnlyList<CalendarNameConflict> Conflicts
        {
            get
            {
                return mConflicts;
            }
        }

        public RecordingConflictResolver(ECalendarNameConflictResolution resolution)
        {
            mResolution = resolution;
        }

        public Task<ECalendarNameConflictResolution> ResolveAsync(CalendarNameConflict conflict, CancellationToken cancellationToken)
        {
            CallCount++;
            ConflictOrNull = conflict;
            mConflicts.Add(conflict);
            return Task.FromResult(mResolution);
        }
    }

    private sealed class SequencedConflictResolver : ICalendarNameConflictResolver
    {
        private readonly Queue<ECalendarNameConflictResolution> mResolutions;

        public int CallCount { get; private set; }

        public SequencedConflictResolver(params ECalendarNameConflictResolution[] resolutions)
        {
            if (resolutions == null || resolutions.Length == 0)
            {
                throw new ArgumentException("At least one conflict resolution is required.", nameof(resolutions));
            }

            mResolutions = new Queue<ECalendarNameConflictResolution>(resolutions);
        }

        public Task<ECalendarNameConflictResolution> ResolveAsync(CalendarNameConflict conflict, CancellationToken cancellationToken)
        {
            if (conflict == null)
            {
                throw new ArgumentNullException(nameof(conflict));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (mResolutions.Count == 0)
            {
                throw new InvalidOperationException("No recorded conflict resolution remains.");
            }

            CallCount++;
            return Task.FromResult(mResolutions.Dequeue());
        }
    }
}
