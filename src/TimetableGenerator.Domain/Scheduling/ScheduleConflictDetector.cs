using System;

namespace TimetableGenerator.Domain.Scheduling;

public static class ScheduleConflictDetector
{
    public static bool HasConflict(
        ScheduledOffering firstOffering,
        ScheduledOffering secondOffering)
    {
        if (firstOffering == null)
        {
            throw new ArgumentNullException(nameof(firstOffering));
        }

        if (secondOffering == null)
        {
            throw new ArgumentNullException(nameof(secondOffering));
        }

        foreach (MeetingSlot firstSlot in firstOffering.MeetingSlots)
        {
            foreach (MeetingSlot secondSlot in secondOffering.MeetingSlots)
            {
                if (firstSlot == secondSlot)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
