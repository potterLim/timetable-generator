using System;
using System.Collections.Generic;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class MeetingSchedule
{
    private readonly KoreanScheduleText? mSourceTextOrNull;
    private readonly IReadOnlyList<MeetingSlot> mSlots;

    public static MeetingSchedule NotProvided { get; } = new MeetingSchedule(EMeetingScheduleStatus.NotProvided, null, Array.Empty<MeetingSlot>());

    public EMeetingScheduleStatus Status { get; }

    public bool HasSourceText
    {
        get
        {
            return mSourceTextOrNull != null;
        }
    }

    public IReadOnlyList<MeetingSlot> Slots
    {
        get
        {
            return mSlots;
        }
    }

    private MeetingSchedule(
        EMeetingScheduleStatus status,
        KoreanScheduleText? sourceTextOrNull,
        IEnumerable<MeetingSlot> slots)
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
            if (slot.Period.Value <= 0)
            {
                throw new ArgumentException("Meeting schedules cannot contain invalid slots.", nameof(slots));
            }

            if (uniqueSlots.Add(slot) == false)
            {
                throw new ArgumentException("Meeting schedules cannot contain duplicate slots.", nameof(slots));
            }

            copiedSlots.Add(slot);
        }

        bool hasScheduledValues = sourceTextOrNull != null && copiedSlots.Count > 0;
        if ((status == EMeetingScheduleStatus.Scheduled) != hasScheduledValues)
        {
            throw new ArgumentException("Scheduled meetings require source text and at least one slot.");
        }

        Status = status;
        mSourceTextOrNull = sourceTextOrNull;
        mSlots = copiedSlots.AsReadOnly();
    }

    public static MeetingSchedule CreateScheduled(
        KoreanScheduleText sourceText,
        IEnumerable<MeetingSlot> slots)
    {
        if (sourceText == null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        return new MeetingSchedule(EMeetingScheduleStatus.Scheduled, sourceText, slots);
    }

    public KoreanScheduleText GetSourceText()
    {
        if (mSourceTextOrNull == null)
        {
            throw new InvalidOperationException("A meeting without a provided schedule has no source text.");
        }

        return mSourceTextOrNull;
    }
}
