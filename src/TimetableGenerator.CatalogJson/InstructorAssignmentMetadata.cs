using System;

namespace TimetableGenerator.CatalogJson;

public sealed class InstructorAssignmentMetadata
{
    private readonly InstructorDisplayText? mDisplayTextOrNull;

    private readonly AdditionalInstructorCount? mAdditionalInstructorCountOrNull;

    public static InstructorAssignmentMetadata Unconfirmed { get; } = new InstructorAssignmentMetadata(EInstructorAssignmentStatus.Unconfirmed, null, null);

    public static InstructorAssignmentMetadata NotProvided { get; } = new InstructorAssignmentMetadata(EInstructorAssignmentStatus.NotProvided, null, null);

    public EInstructorAssignmentStatus Status { get; }

    public bool HasConfirmedInstructor
    {
        get
        {
            return Status == EInstructorAssignmentStatus.Confirmed;
        }
    }

    private InstructorAssignmentMetadata(EInstructorAssignmentStatus status, InstructorDisplayText? displayTextOrNull, AdditionalInstructorCount? additionalInstructorCountOrNull)
    {
        bool hasConfirmedValues = displayTextOrNull != null && additionalInstructorCountOrNull.HasValue;
        if ((status == EInstructorAssignmentStatus.Confirmed) != hasConfirmedValues)
        {
            throw new ArgumentException("Confirmed instructors require display text and an additional instructor count.");
        }

        Status = status;
        mDisplayTextOrNull = displayTextOrNull;
        mAdditionalInstructorCountOrNull = additionalInstructorCountOrNull;
    }

    public static InstructorAssignmentMetadata CreateConfirmed(InstructorDisplayText displayText, AdditionalInstructorCount additionalInstructorCount)
    {
        if (displayText == null)
        {
            throw new ArgumentNullException(nameof(displayText));
        }

        return new InstructorAssignmentMetadata(EInstructorAssignmentStatus.Confirmed, displayText, additionalInstructorCount);
    }

    public InstructorDisplayText GetDisplayText()
    {
        if (mDisplayTextOrNull == null)
        {
            throw new InvalidOperationException("No confirmed instructor display text is available.");
        }

        return mDisplayTextOrNull;
    }

    public AdditionalInstructorCount GetAdditionalInstructorCount()
    {
        if (mAdditionalInstructorCountOrNull.HasValue == false)
        {
            throw new InvalidOperationException("No additional instructor count is available.");
        }

        return mAdditionalInstructorCountOrNull.Value;
    }
}
