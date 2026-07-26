using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class LocationAssignment
{
    private readonly ClassroomDisplayText? mDisplayTextOrNull;

    public static LocationAssignment NotProvided { get; } = new LocationAssignment(ELocationAssignmentStatus.NotProvided, null);

    public ELocationAssignmentStatus Status { get; }

    public bool HasDisplayText
    {
        get
        {
            return mDisplayTextOrNull != null;
        }
    }

    private LocationAssignment(
        ELocationAssignmentStatus status,
        ClassroomDisplayText? displayTextOrNull)
    {
        if (Enum.IsDefined(typeof(ELocationAssignmentStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool hasAssignedValue = displayTextOrNull != null;
        if ((status == ELocationAssignmentStatus.Assigned) != hasAssignedValue)
        {
            throw new ArgumentException("Assigned locations require display text.");
        }

        Status = status;
        mDisplayTextOrNull = displayTextOrNull;
    }

    public static LocationAssignment CreateAssigned(ClassroomDisplayText displayText)
    {
        if (displayText == null)
        {
            throw new ArgumentNullException(nameof(displayText));
        }

        return new LocationAssignment(ELocationAssignmentStatus.Assigned, displayText);
    }

    public ClassroomDisplayText GetDisplayText()
    {
        if (mDisplayTextOrNull == null)
        {
            throw new InvalidOperationException("A missing location has no display text.");
        }

        return mDisplayTextOrNull;
    }
}
