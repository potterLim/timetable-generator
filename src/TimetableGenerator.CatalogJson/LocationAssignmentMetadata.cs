using System;

namespace TimetableGenerator.CatalogJson;

public sealed class LocationAssignmentMetadata
{
    private readonly ClassroomDisplayText? mDisplayTextOrNull;

    public static LocationAssignmentMetadata NotProvided { get; } = new LocationAssignmentMetadata(ELocationAssignmentStatus.NotProvided, null);

    public ELocationAssignmentStatus Status { get; }

    public bool IsAssigned
    {
        get
        {
            return Status == ELocationAssignmentStatus.Assigned;
        }
    }

    private LocationAssignmentMetadata(ELocationAssignmentStatus status, ClassroomDisplayText? displayTextOrNull)
    {
        bool hasAssignedValue = displayTextOrNull != null;
        if ((status == ELocationAssignmentStatus.Assigned) != hasAssignedValue)
        {
            throw new ArgumentException("Assigned locations require display text.");
        }

        Status = status;
        mDisplayTextOrNull = displayTextOrNull;
    }

    public static LocationAssignmentMetadata CreateAssigned(ClassroomDisplayText displayText)
    {
        if (displayText == null)
        {
            throw new ArgumentNullException(nameof(displayText));
        }

        return new LocationAssignmentMetadata(ELocationAssignmentStatus.Assigned, displayText);
    }

    public ClassroomDisplayText GetDisplayText()
    {
        if (mDisplayTextOrNull == null)
        {
            throw new InvalidOperationException("No assigned classroom display text is available.");
        }

        return mDisplayTextOrNull;
    }
}
