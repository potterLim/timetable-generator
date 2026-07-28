using System;
using System.Collections.Generic;

namespace TimetableGenerator.Domain.Scheduling;

public sealed class MeetingSchedule
{
    private readonly IReadOnlyList<MeetingSlot> mSlots;

    public static MeetingSchedule NotProvided { get; } = new MeetingSchedule(EMeetingScheduleStatus.NotProvided, Array.Empty<MeetingSlot>());

    public EMeetingScheduleStatus Status { get; }

    public bool IsScheduled
    {
        get
        {
            return Status == EMeetingScheduleStatus.Scheduled;
        }
    }

    public IReadOnlyList<MeetingSlot> Slots
    {
        get
        {
            return mSlots;
        }
    }

    private MeetingSchedule(EMeetingScheduleStatus status, IEnumerable<MeetingSlot> slots)
    {
        if (Enum.IsDefined(typeof(EMeetingScheduleStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (slots == null)
        {
            throw new ArgumentNullException(nameof(slots));
        }

        List<MeetingSlot> copiedSlots = new List<MeetingSlot>();
        HashSet<MeetingSlot> uniqueSlots = new HashSet<MeetingSlot>();
        foreach (MeetingSlot slot in slots)
        {
            if (slot.IsValid == false)
            {
                throw new ArgumentException("Meeting schedules cannot contain invalid slots.", nameof(slots));
            }

            if (uniqueSlots.Add(slot) == false)
            {
                throw new ArgumentException("Meeting schedules cannot contain duplicate slots.", nameof(slots));
            }

            copiedSlots.Add(slot);
        }

        bool hasScheduledSlots = copiedSlots.Count > 0;
        if ((status == EMeetingScheduleStatus.Scheduled) != hasScheduledSlots)
        {
            throw new ArgumentException("Scheduled meetings require slots and meetings without provided times cannot contain slots.");
        }

        Status = status;
        mSlots = copiedSlots.AsReadOnly();
    }

    public static MeetingSchedule CreateScheduled(IEnumerable<MeetingSlot> slots)
    {
        return new MeetingSchedule(EMeetingScheduleStatus.Scheduled, slots);
    }
}
