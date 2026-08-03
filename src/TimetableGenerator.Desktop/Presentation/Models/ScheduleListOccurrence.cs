using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleListOccurrence
{
    private readonly IReadOnlyList<EDay> mDays;

    private readonly IReadOnlyList<ScheduleListSource> mSources;

    public IReadOnlyList<EDay> Days
    {
        get
        {
            return mDays;
        }
    }

    public EDay EarliestDay
    {
        get
        {
            return mDays[0];
        }
    }

    public DailyTimeRange TimeRange { get; }

    public string ScheduleDisplayText { get; }

    public ScheduleListMetadata Metadata { get; }

    public string SectionDisplayText
    {
        get
        {
            return Metadata.SectionDisplayText;
        }
    }

    public string LocationDisplayText
    {
        get
        {
            return Metadata.LocationDisplayText;
        }
    }

    public string ResponsiblePersonDisplayText
    {
        get
        {
            return Metadata.ResponsiblePersonDisplayText;
        }
    }

    public string MetadataDisplayText
    {
        get
        {
            return Metadata.DisplayText;
        }
    }

    public IReadOnlyList<ScheduleListSource> Sources
    {
        get
        {
            return mSources;
        }
    }

    public bool HasSection
    {
        get
        {
            return Metadata.HasSection;
        }
    }

    public bool HasLocation
    {
        get
        {
            return Metadata.HasLocation;
        }
    }

    public bool HasResponsiblePerson
    {
        get
        {
            return Metadata.HasResponsiblePerson;
        }
    }

    public bool HasMetadata
    {
        get
        {
            return Metadata.HasDisplayText;
        }
    }

    public string AccessibleName
    {
        get
        {
            List<string> details = new List<string>();
            details.Add(createAccessibleScheduleText(mDays, TimeRange));
            if (HasSection)
            {
                details.Add("분반 " + SectionDisplayText);
            }

            if (HasLocation)
            {
                details.Add("장소 " + LocationDisplayText);
            }

            if (HasResponsiblePerson)
            {
                details.Add("담당 " + ResponsiblePersonDisplayText);
            }

            return string.Join(", ", details);
        }
    }

    internal ScheduleListOccurrence(IReadOnlyList<EDay> days, DailyTimeRange timeRange, ScheduleListMetadata metadata, IReadOnlyList<ScheduleListSource> sources)
    {
        if (days == null)
        {
            throw new ArgumentNullException(nameof(days));
        }

        if (days.Count == 0)
        {
            throw new ArgumentException("Schedule list occurrences require at least one day.", nameof(days));
        }

        if (timeRange.IsValid == false)
        {
            throw new ArgumentException("Schedule list occurrences require a valid time range.", nameof(timeRange));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (sources == null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (sources.Count == 0)
        {
            throw new ArgumentException("Schedule list occurrences require at least one source.", nameof(sources));
        }

        mDays = copyAndValidateDays(days);
        mSources = new List<ScheduleListSource>(sources).AsReadOnly();
        TimeRange = timeRange;
        Metadata = metadata;
        ScheduleDisplayText = ScheduleBoardDayRange.CreateShortDayTimeDisplayText(mDays, timeRange);
    }

    private static IReadOnlyList<EDay> copyAndValidateDays(IReadOnlyList<EDay> days)
    {
        List<EDay> copiedDays = new List<EDay>(days.Count);
        foreach (EDay day in days)
        {
            ensureDefinedDay(day);
            if (copiedDays.Contains(day) == false)
            {
                copiedDays.Add(day);
            }
        }

        copiedDays.Sort();
        return copiedDays.AsReadOnly();
    }

    private static string createAccessibleScheduleText(IReadOnlyList<EDay> days, DailyTimeRange timeRange)
    {
        List<string> dayNames = new List<string>(days.Count);
        foreach (EDay day in days)
        {
            dayNames.Add(ScheduleBoardDayRange.FindFullDayDisplayName(day));
        }

        return string.Join(", ", dayNames) + " " + timeRange;
    }

    private static void ensureDefinedDay(EDay day)
    {
        ScheduleBoardDayRange.FindFullDayDisplayName(day);
    }
}
