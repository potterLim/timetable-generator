using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PersonalScheduleListSource : ScheduleListSource
{
    public PersonalScheduleId ScheduleId { get; }

    public PersonalScheduleListSource(PersonalScheduleId scheduleId)
        : base(EScheduleListEntryKind.PersonalSchedule)
    {
        if (scheduleId.IsValid == false)
        {
            throw new System.ArgumentException("Personal schedule list sources require a valid schedule ID.", nameof(scheduleId));
        }

        ScheduleId = scheduleId;
    }

    internal override bool hasSameIdentityAs(ScheduleListSource other)
    {
        PersonalScheduleListSource? personalSourceOrNull = other as PersonalScheduleListSource;
        return personalSourceOrNull != null && personalSourceOrNull.ScheduleId == ScheduleId;
    }
}
