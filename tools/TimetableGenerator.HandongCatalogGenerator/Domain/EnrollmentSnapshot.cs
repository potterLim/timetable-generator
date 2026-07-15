using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class EnrollmentSnapshot
{
    private readonly EnrollmentCount? mCountOrNull;

    public static EnrollmentSnapshot NotProvided { get; } = new EnrollmentSnapshot(
        EEnrollmentStatus.NotProvided,
        null);

    public EEnrollmentStatus Status { get; }

    public bool HasCount
    {
        get
        {
            return mCountOrNull.HasValue;
        }
    }

    private EnrollmentSnapshot(EEnrollmentStatus status, EnrollmentCount? countOrNull)
    {
        if (Enum.IsDefined(typeof(EEnrollmentStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if ((status == EEnrollmentStatus.Provided) != countOrNull.HasValue)
        {
            throw new ArgumentException("Provided enrollment snapshots require a count.");
        }

        Status = status;
        mCountOrNull = countOrNull;
    }

    public static EnrollmentSnapshot CreateProvided(EnrollmentCount count)
    {
        return new EnrollmentSnapshot(EEnrollmentStatus.Provided, count);
    }

    public EnrollmentCount GetCount()
    {
        if (mCountOrNull.HasValue == false)
        {
            throw new InvalidOperationException("A missing enrollment snapshot has no count.");
        }

        return mCountOrNull.Value;
    }
}
