using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class OfferingCapacity
{
    public SeatCapacity SeatCapacity { get; }

    public EnrollmentSnapshot Enrollment { get; }

    public OfferingCapacity(SeatCapacity seatCapacity, EnrollmentSnapshot enrollment)
    {
        if (enrollment == null)
        {
            throw new ArgumentNullException(nameof(enrollment));
        }

        SeatCapacity = seatCapacity;
        Enrollment = enrollment;
    }
}
