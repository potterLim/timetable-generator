namespace TimetableGenerator.Desktop.Presentation.Models;

internal abstract class ScheduleListSource
{
    public EScheduleListEntryKind Kind { get; }

    protected ScheduleListSource(EScheduleListEntryKind kind)
    {
        Kind = kind;
    }

    internal abstract bool hasSameIdentityAs(ScheduleListSource other);
}
