using System;
using System.Collections.Generic;
using System.Text;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal static partial class ScheduleListProjector
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
