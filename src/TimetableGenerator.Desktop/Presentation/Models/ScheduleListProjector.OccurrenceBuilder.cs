using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal static partial class ScheduleListProjector
{
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
}
