using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class InstructorAssignment
{
    private readonly InstructorDisplayText? mDisplayTextOrNull;
    private readonly AdditionalInstructorCount? mAdditionalInstructorCountOrNull;

    public static InstructorAssignment Unconfirmed { get; } = new InstructorAssignment(
        EInstructorAssignmentStatus.Unconfirmed,
        null,
        null);

    public static InstructorAssignment NotProvided { get; } = new InstructorAssignment(
        EInstructorAssignmentStatus.NotProvided,
        null,
        null);

    public EInstructorAssignmentStatus Status { get; }

    public bool HasDisplayText
    {
        get
        {
            return mDisplayTextOrNull != null;
        }
    }

    public bool HasAdditionalInstructorCount
    {
        get
        {
            return mAdditionalInstructorCountOrNull.HasValue;
        }
    }

    private InstructorAssignment(
        EInstructorAssignmentStatus status,
        InstructorDisplayText? displayTextOrNull,
        AdditionalInstructorCount? additionalInstructorCountOrNull)
    {
        if (Enum.IsDefined(typeof(EInstructorAssignmentStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool hasConfirmedValues =
            displayTextOrNull != null && additionalInstructorCountOrNull.HasValue;
        if ((status == EInstructorAssignmentStatus.Confirmed) != hasConfirmedValues)
        {
            throw new ArgumentException(
                "Only confirmed instructor assignments can contain instructor values.");
        }

        Status = status;
        mDisplayTextOrNull = displayTextOrNull;
        mAdditionalInstructorCountOrNull = additionalInstructorCountOrNull;
    }

    public static InstructorAssignment CreateConfirmed(
        InstructorDisplayText displayText,
        AdditionalInstructorCount additionalInstructorCount)
    {
        if (displayText == null)
        {
            throw new ArgumentNullException(nameof(displayText));
        }

        return new InstructorAssignment(
            EInstructorAssignmentStatus.Confirmed,
            displayText,
            additionalInstructorCount);
    }

    public InstructorDisplayText GetDisplayText()
    {
        if (mDisplayTextOrNull == null)
        {
            throw new InvalidOperationException(
                "An unconfirmed or missing instructor assignment has no display text.");
        }

        return mDisplayTextOrNull;
    }

    public AdditionalInstructorCount GetAdditionalInstructorCount()
    {
        if (mAdditionalInstructorCountOrNull.HasValue == false)
        {
            throw new InvalidOperationException(
                "An unconfirmed or missing instructor assignment has no instructor count.");
        }

        return mAdditionalInstructorCountOrNull.Value;
    }
}
