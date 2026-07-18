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

    public string SectionDisplayText { get; }

    public string LocationDisplayText { get; }

    public string ResponsiblePersonDisplayText { get; }

    public string MetadataDisplayText { get; }

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
            return string.IsNullOrWhiteSpace(SectionDisplayText) == false;
        }
    }

    public bool HasLocation
    {
        get
        {
            return string.IsNullOrWhiteSpace(LocationDisplayText) == false;
        }
    }

    public bool HasResponsiblePerson
    {
        get
        {
            return string.IsNullOrWhiteSpace(ResponsiblePersonDisplayText) == false;
        }
    }

    public bool HasMetadata
    {
        get
        {
            return string.IsNullOrWhiteSpace(MetadataDisplayText) == false;
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

    internal ScheduleListOccurrence(
        IReadOnlyList<EDay> days,
        DailyTimeRange timeRange,
        string sectionDisplayText,
        string locationDisplayText,
        string responsiblePersonDisplayText,
        IReadOnlyList<ScheduleListSource> sources,
        bool includeSectionInMetadata)
    {
        if (days == null)
        {
            throw new ArgumentNullException(nameof(days));
        }

        if (days.Count == 0)
        {
            throw new ArgumentException(
                "Schedule list occurrences require at least one day.",
                nameof(days));
        }

        if (timeRange.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule list occurrences require a valid time range.",
                nameof(timeRange));
        }

        if (sectionDisplayText == null)
        {
            throw new ArgumentNullException(nameof(sectionDisplayText));
        }

        if (locationDisplayText == null)
        {
            throw new ArgumentNullException(nameof(locationDisplayText));
        }

        if (responsiblePersonDisplayText == null)
        {
            throw new ArgumentNullException(nameof(responsiblePersonDisplayText));
        }

        if (sources == null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (sources.Count == 0)
        {
            throw new ArgumentException(
                "Schedule list occurrences require at least one source.",
                nameof(sources));
        }

        mDays = copyAndValidateDays(days);
        mSources = new List<ScheduleListSource>(sources).AsReadOnly();
        TimeRange = timeRange;
        SectionDisplayText = sectionDisplayText;
        LocationDisplayText = locationDisplayText;
        ResponsiblePersonDisplayText = responsiblePersonDisplayText;
        ScheduleDisplayText = createScheduleDisplayText(mDays, timeRange);
        MetadataDisplayText = createMetadataDisplayText(
            sectionDisplayText,
            locationDisplayText,
            responsiblePersonDisplayText,
            includeSectionInMetadata);
    }

    private static IReadOnlyList<EDay> copyAndValidateDays(
        IReadOnlyList<EDay> days)
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

    private static string createScheduleDisplayText(
        IReadOnlyList<EDay> days,
        DailyTimeRange timeRange)
    {
        return ScheduleBoardDayRange.CreateShortDayTimeDisplayText(
            days,
            timeRange);
    }

    private static string createAccessibleScheduleText(
        IReadOnlyList<EDay> days,
        DailyTimeRange timeRange)
    {
        List<string> dayNames = new List<string>(days.Count);
        foreach (EDay day in days)
        {
            dayNames.Add(ScheduleBoardDayRange.FindFullDayDisplayName(day));
        }

        return string.Join(", ", dayNames) + " " + timeRange;
    }

    private static string createMetadataDisplayText(
        string sectionDisplayText,
        string locationDisplayText,
        string responsiblePersonDisplayText,
        bool includeSection)
    {
        List<string> metadata = new List<string>();
        if (includeSection && string.IsNullOrWhiteSpace(sectionDisplayText) == false)
        {
            metadata.Add("(" + sectionDisplayText + ")");
        }

        if (string.IsNullOrWhiteSpace(locationDisplayText) == false)
        {
            metadata.Add(locationDisplayText);
        }

        if (string.IsNullOrWhiteSpace(responsiblePersonDisplayText) == false)
        {
            metadata.Add(responsiblePersonDisplayText);
        }

        return string.Join(" · ", metadata);
    }

    private static void ensureDefinedDay(EDay day)
    {
        ScheduleBoardDayRange.FindFullDayDisplayName(day);
    }
}
