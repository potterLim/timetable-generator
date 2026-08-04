using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal static partial class ScheduleListProjector
{
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
                ScheduleListOccurrence occurrence;
                if (sharedSectionOrNull == null)
                {
                    occurrence = occurrenceBuilder.Build();
                }
                else
                {
                    occurrence = occurrenceBuilder.BuildWithSectionInTitle();
                }
                occurrences.Add(occurrence);
            }

            string titleDisplayText;
            if (sharedSectionOrNull != null)
            {
                titleDisplayText = mTitle + "(" + sharedSectionOrNull + ")";
            }
            else
            {
                titleDisplayText = mTitle;
            }
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
}
