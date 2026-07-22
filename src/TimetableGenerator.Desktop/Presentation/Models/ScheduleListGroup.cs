using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleListGroup
{
    private readonly IReadOnlyList<ScheduleListOccurrence> mOccurrences;

    private readonly IReadOnlyList<ScheduleListSource> mSources;

    public string Title { get; }

    public string TitleDisplayText { get; }

    public IReadOnlyList<ScheduleListOccurrence> Occurrences
    {
        get
        {
            return mOccurrences;
        }
    }

    public IReadOnlyList<ScheduleListSource> Sources
    {
        get
        {
            return mSources;
        }
    }

    public EDay EarliestDay
    {
        get
        {
            return mOccurrences[0].EarliestDay;
        }
    }

    public DailyTimeRange EarliestTimeRange
    {
        get
        {
            return mOccurrences[0].TimeRange;
        }
    }

    public bool HasMultipleOccurrences
    {
        get
        {
            return mOccurrences.Count > 1;
        }
    }

    public string AccessibleName
    {
        get
        {
            List<string> details = new List<string>();
            details.Add(findSourceKindDisplayText(mSources));
            details.Add(TitleDisplayText);
            foreach (ScheduleListOccurrence occurrence in mOccurrences)
            {
                details.Add(occurrence.AccessibleName);
            }

            return string.Join(", ", details);
        }
    }

    internal ScheduleListGroup(
        string title,
        string titleDisplayText,
        IReadOnlyList<ScheduleListOccurrence> occurrences,
        IReadOnlyList<ScheduleListSource> sources)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Schedule list groups require a title.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(titleDisplayText))
        {
            throw new ArgumentException("Schedule list groups require display text.", nameof(titleDisplayText));
        }

        if (occurrences == null)
        {
            throw new ArgumentNullException(nameof(occurrences));
        }

        if (occurrences.Count == 0)
        {
            throw new ArgumentException(
                "Schedule list groups require at least one occurrence.",
                nameof(occurrences));
        }

        if (sources == null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (sources.Count == 0)
        {
            throw new ArgumentException("Schedule list groups require at least one source.", nameof(sources));
        }

        Title = title;
        TitleDisplayText = titleDisplayText;
        mOccurrences = new List<ScheduleListOccurrence>(occurrences).AsReadOnly();
        mSources = new List<ScheduleListSource>(sources).AsReadOnly();
    }

    private static string findSourceKindDisplayText(IReadOnlyList<ScheduleListSource> sources)
    {
        bool hasCourse = false;
        bool hasPersonalSchedule = false;
        foreach (ScheduleListSource source in sources)
        {
            hasCourse |= source.Kind == EScheduleListEntryKind.Course;
            hasPersonalSchedule |= source.Kind == EScheduleListEntryKind.PersonalSchedule;
        }

        if (hasCourse && hasPersonalSchedule)
        {
            return "과목 및 개인 일정";
        }

        return hasCourse ? "과목" : "개인 일정";
    }
}
