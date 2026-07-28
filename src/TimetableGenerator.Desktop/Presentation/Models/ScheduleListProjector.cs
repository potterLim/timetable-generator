using System;
using System.Collections.Generic;
using System.Text;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal static class ScheduleListProjector
{
    public static IReadOnlyList<ScheduleListGroup> Project(IReadOnlyList<ScheduleEntry> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        Dictionary<string, ScheduleListGroupBuilder> buildersByTitle = new Dictionary<string, ScheduleListGroupBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (ScheduleEntry entry in entries)
        {
            if (entry == null)
            {
                throw new ArgumentException("Schedule list projections cannot contain null entries.", nameof(entries));
            }

            ScheduleListProjectionItem item = createProjectionItem(entry);
            string normalizedTitle = normalizeTitle(item.Title);
            ScheduleListGroupBuilder? builderOrNull;
            if (buildersByTitle.TryGetValue(normalizedTitle, out builderOrNull) == false)
            {
                builderOrNull = new ScheduleListGroupBuilder(normalizedTitle);
                buildersByTitle.Add(normalizedTitle, builderOrNull);
            }

            builderOrNull.Add(item);
        }

        List<ScheduleListGroup> groups = new List<ScheduleListGroup>(buildersByTitle.Count);
        foreach (ScheduleListGroupBuilder builder in buildersByTitle.Values)
        {
            groups.Add(builder.Build());
        }

        groups.Sort(compareGroups);
        return groups.AsReadOnly();
    }

    private static ScheduleListProjectionItem createProjectionItem(ScheduleEntry entry)
    {
        CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
        if (courseEntryOrNull != null)
        {
            return new ScheduleListProjectionItem(
                courseEntryOrNull.Name,
                courseEntryOrNull.Day,
                courseEntryOrNull.TimeRange,
                new ScheduleListMetadata(courseEntryOrNull),
                new CourseScheduleListSource(courseEntryOrNull.CourseId, courseEntryOrNull.OfferingId));
        }

        PersonalScheduleEntry? personalEntryOrNull = entry as PersonalScheduleEntry;
        if (personalEntryOrNull != null)
        {
            return new ScheduleListProjectionItem(
                personalEntryOrNull.Title,
                personalEntryOrNull.Day,
                personalEntryOrNull.TimeRange,
                new ScheduleListMetadata(personalEntryOrNull),
                new PersonalScheduleListSource(personalEntryOrNull.ScheduleId));
        }

        throw new InvalidOperationException("Schedule lists require a supported schedule entry type.");
    }

    private static string normalizeTitle(string title)
    {
        string compatibilityNormalizedTitle = title.Normalize(NormalizationForm.FormKC);
        StringBuilder normalizedTitle = new StringBuilder(compatibilityNormalizedTitle.Length);
        bool isPendingSpace = false;
        foreach (char character in compatibilityNormalizedTitle)
        {
            if (char.IsWhiteSpace(character))
            {
                isPendingSpace = normalizedTitle.Length > 0;
                continue;
            }

            if (isPendingSpace)
            {
                normalizedTitle.Append(' ');
                isPendingSpace = false;
            }

            normalizedTitle.Append(character);
        }

        if (normalizedTitle.Length == 0)
        {
            throw new InvalidOperationException("Schedule list entries require a non-empty title.");
        }

        return normalizedTitle.ToString();
    }

    private static int compareGroups(ScheduleListGroup left, ScheduleListGroup right)
    {
        int dayComparison = left.EarliestDay.CompareTo(right.EarliestDay);
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        int timeComparison = left.EarliestTimeRange.Start.CompareTo(right.EarliestTimeRange.Start);
        if (timeComparison != 0)
        {
            return timeComparison;
        }

        return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ScheduleListGroupBuilder
    {
        private string mTitle;

        private bool mHasCourseTitle;

        private readonly List<ScheduleListOccurrenceBuilder> mOccurrenceBuilders;

        public ScheduleListGroupBuilder(string title)
        {
            mTitle = title;
            mOccurrenceBuilders = new List<ScheduleListOccurrenceBuilder>();
        }

        public void Add(ScheduleListProjectionItem item)
        {
            if (mHasCourseTitle == false && item.Source.Kind == EScheduleListEntryKind.Course)
            {
                mTitle = normalizeTitle(item.Title);
                mHasCourseTitle = true;
            }

            ScheduleListOccurrenceBuilder? matchingBuilderOrNull = null;
            foreach (ScheduleListOccurrenceBuilder occurrenceBuilder in mOccurrenceBuilders)
            {
                if (occurrenceBuilder.HasSamePresentationMetadata(item))
                {
                    matchingBuilderOrNull = occurrenceBuilder;
                    break;
                }
            }

            if (matchingBuilderOrNull == null)
            {
                matchingBuilderOrNull = new ScheduleListOccurrenceBuilder(item);
                mOccurrenceBuilders.Add(matchingBuilderOrNull);
                return;
            }

            matchingBuilderOrNull.Add(item);
        }

        public ScheduleListGroup Build()
        {
            mOccurrenceBuilders.Sort(compareOccurrenceBuilders);
            List<ScheduleListSource> groupSources = collectUniqueSources();
            string? sharedSectionOrNull = findSharedSectionOrNull();
            List<ScheduleListOccurrence> occurrences = new List<ScheduleListOccurrence>(mOccurrenceBuilders.Count);
            foreach (ScheduleListOccurrenceBuilder occurrenceBuilder in mOccurrenceBuilders)
            {
                ScheduleListOccurrence occurrence = sharedSectionOrNull == null ? occurrenceBuilder.Build() : occurrenceBuilder.BuildWithSectionInTitle();
                occurrences.Add(occurrence);
            }

            string titleDisplayText = sharedSectionOrNull == null ? mTitle : mTitle + "(" + sharedSectionOrNull + ")";
            return new ScheduleListGroup(mTitle, titleDisplayText, occurrences.AsReadOnly(), groupSources.AsReadOnly());
        }

        private List<ScheduleListSource> collectUniqueSources()
        {
            List<ScheduleListSource> sources = new List<ScheduleListSource>();
            foreach (ScheduleListOccurrenceBuilder occurrenceBuilder in mOccurrenceBuilders)
            {
                foreach (ScheduleListSource source in occurrenceBuilder.Sources)
                {
                    addUniqueSource(sources, source);
                }
            }

            sources.Sort(compareSources);
            return sources;
        }

        private string? findSharedSectionOrNull()
        {
            string? sharedSectionOrNull = null;
            foreach (ScheduleListOccurrenceBuilder occurrenceBuilder in mOccurrenceBuilders)
            {
                if (string.IsNullOrWhiteSpace(occurrenceBuilder.Metadata.SectionDisplayText))
                {
                    return null;
                }

                if (sharedSectionOrNull == null)
                {
                    sharedSectionOrNull = occurrenceBuilder.Metadata.SectionDisplayText;
                    continue;
                }

                if (string.Equals(sharedSectionOrNull, occurrenceBuilder.Metadata.SectionDisplayText, StringComparison.Ordinal) == false)
                {
                    return null;
                }
            }

            return sharedSectionOrNull;
        }

        private static int compareOccurrenceBuilders(ScheduleListOccurrenceBuilder left, ScheduleListOccurrenceBuilder right)
        {
            int dayComparison = left.EarliestDay.CompareTo(right.EarliestDay);
            if (dayComparison != 0)
            {
                return dayComparison;
            }

            int timeComparison = left.TimeRange.Start.CompareTo(right.TimeRange.Start);
            if (timeComparison != 0)
            {
                return timeComparison;
            }

            int sectionComparison = string.Compare(left.Metadata.SectionDisplayText, right.Metadata.SectionDisplayText, StringComparison.Ordinal);
            if (sectionComparison != 0)
            {
                return sectionComparison;
            }

            int locationComparison = string.Compare(left.Metadata.LocationDisplayText, right.Metadata.LocationDisplayText, StringComparison.Ordinal);
            if (locationComparison != 0)
            {
                return locationComparison;
            }

            return string.Compare(left.Metadata.ResponsiblePersonDisplayText, right.Metadata.ResponsiblePersonDisplayText, StringComparison.Ordinal);
        }
    }

    private sealed class ScheduleListOccurrenceBuilder
    {
        private readonly List<EDay> mDays;

        private readonly List<ScheduleListSource> mSources;

        public EDay EarliestDay
        {
            get
            {
                EDay earliestDay = mDays[0];
                foreach (EDay day in mDays)
                {
                    if (day < earliestDay)
                    {
                        earliestDay = day;
                    }
                }

                return earliestDay;
            }
        }

        public DailyTimeRange TimeRange { get; }

        public ScheduleListMetadata Metadata { get; }

        public IReadOnlyList<ScheduleListSource> Sources
        {
            get
            {
                return mSources;
            }
        }

        public ScheduleListOccurrenceBuilder(ScheduleListProjectionItem item)
        {
            mDays = new List<EDay>();
            mSources = new List<ScheduleListSource>();
            TimeRange = item.TimeRange;
            Metadata = item.Metadata;
            Add(item);
        }

        public bool HasSamePresentationMetadata(ScheduleListProjectionItem item)
        {
            return TimeRange == item.TimeRange && Metadata.HasSameContentAs(item.Metadata);
        }

        public void Add(ScheduleListProjectionItem item)
        {
            if (HasSamePresentationMetadata(item) == false)
            {
                throw new ArgumentException("Only matching schedule metadata can share an occurrence.", nameof(item));
            }

            if (mDays.Contains(item.Day) == false)
            {
                mDays.Add(item.Day);
            }

            addUniqueSource(mSources, item.Source);
        }

        public ScheduleListOccurrence Build()
        {
            return build(Metadata);
        }

        public ScheduleListOccurrence BuildWithSectionInTitle()
        {
            return build(Metadata.WithoutSectionInDisplay());
        }

        private ScheduleListOccurrence build(ScheduleListMetadata metadata)
        {
            List<ScheduleListSource> orderedSources = new List<ScheduleListSource>(mSources);
            orderedSources.Sort(compareSources);
            return new ScheduleListOccurrence(mDays.AsReadOnly(), TimeRange, metadata, orderedSources.AsReadOnly());
        }
    }

    private sealed class ScheduleListProjectionItem
    {
        public string Title { get; }

        public EDay Day { get; }

        public DailyTimeRange TimeRange { get; }

        public ScheduleListMetadata Metadata { get; }

        public ScheduleListSource Source { get; }

        public ScheduleListProjectionItem(
            string title,
            EDay day,
            DailyTimeRange timeRange,
            ScheduleListMetadata metadata,
            ScheduleListSource source)
        {
            Title = title;
            Day = day;
            TimeRange = timeRange;
            Metadata = metadata;
            Source = source;
        }
    }

    private static void addUniqueSource(ICollection<ScheduleListSource> sources, ScheduleListSource candidate)
    {
        foreach (ScheduleListSource source in sources)
        {
            if (source.hasSameIdentityAs(candidate))
            {
                return;
            }
        }

        sources.Add(candidate);
    }

    private static int compareSources(ScheduleListSource left, ScheduleListSource right)
    {
        int kindComparison = left.Kind.CompareTo(right.Kind);
        if (kindComparison != 0)
        {
            return kindComparison;
        }

        CourseScheduleListSource? leftCourseOrNull = left as CourseScheduleListSource;
        CourseScheduleListSource? rightCourseOrNull = right as CourseScheduleListSource;
        if (leftCourseOrNull != null && rightCourseOrNull != null)
        {
            int courseComparison = string.Compare(leftCourseOrNull.CourseId.Value, rightCourseOrNull.CourseId.Value, StringComparison.Ordinal);
            if (courseComparison != 0)
            {
                return courseComparison;
            }

            return string.Compare(leftCourseOrNull.OfferingId.Value, rightCourseOrNull.OfferingId.Value, StringComparison.Ordinal);
        }

        PersonalScheduleListSource leftPersonal = (PersonalScheduleListSource)left;
        PersonalScheduleListSource rightPersonal = (PersonalScheduleListSource)right;
        return leftPersonal.ScheduleId.Value.CompareTo(rightPersonal.ScheduleId.Value);
    }
}
