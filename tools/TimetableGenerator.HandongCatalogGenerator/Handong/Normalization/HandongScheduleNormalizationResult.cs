using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongScheduleNormalizationResult
{
    public MeetingSchedule Schedule { get; }

    public EEnglishScheduleComparison EnglishScheduleComparison { get; }

    public HandongScheduleNormalizationResult(
        MeetingSchedule schedule,
        EEnglishScheduleComparison englishScheduleComparison)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        if (Enum.IsDefined(typeof(EEnglishScheduleComparison), englishScheduleComparison) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(englishScheduleComparison));
        }

        if (schedule.Status == EMeetingScheduleStatus.NotProvided &&
            englishScheduleComparison != EEnglishScheduleComparison.NotApplicable)
        {
            throw new ArgumentException(
                "Schedules without meeting data cannot have an English comparison.",
                nameof(englishScheduleComparison));
        }

        Schedule = schedule;
        EnglishScheduleComparison = englishScheduleComparison;
    }
}
