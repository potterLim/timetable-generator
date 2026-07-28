using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal static class ScheduleRecommendationConflictChecker
{
    public static bool CanAddOffering(ScheduleSearchNode node, ScheduledOffering offering, IReadOnlyList<PersonalSchedule> personalSchedules)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (offering == null)
        {
            throw new ArgumentNullException(nameof(offering));
        }

        if (personalSchedules == null)
        {
            throw new ArgumentNullException(nameof(personalSchedules));
        }

        foreach (MeetingSlot slot in offering.MeetingSlots)
        {
            if (node.OccupiedSlots.Contains(slot))
            {
                return false;
            }

            WeeklyTimeRange offeringTimeRange = AcademicPeriodTimeTable.GetWeeklyTimeRange(slot);
            foreach (PersonalSchedule personalSchedule in personalSchedules)
            {
                foreach (WeeklyTimeRange personalTimeRange in personalSchedule.TimeRanges)
                {
                    if (ScheduleConflictDetector.HasConflict(offeringTimeRange, personalTimeRange))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
