using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleRecommendation
{
    private readonly IReadOnlyList<ScheduleEntry> mEntries;

    public IReadOnlyList<ScheduleEntry> Entries
    {
        get
        {
            return mEntries;
        }
    }

    public ScheduleRecommendation(IEnumerable<ScheduleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        mEntries = new List<ScheduleEntry>(entries).AsReadOnly();
    }
}
