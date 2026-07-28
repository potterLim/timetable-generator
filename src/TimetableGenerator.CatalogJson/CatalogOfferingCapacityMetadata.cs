using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogOfferingCapacityMetadata
{
    private readonly OfferingEnrollmentCount? mCurrentEnrollmentOrNull;

    public OfferingSeatCapacity SeatCapacity { get; }

    public bool HasCurrentEnrollment
    {
        get
        {
            return mCurrentEnrollmentOrNull.HasValue;
        }
    }

    private CatalogOfferingCapacityMetadata(OfferingSeatCapacity seatCapacity, OfferingEnrollmentCount? currentEnrollmentOrNull)
    {
        SeatCapacity = seatCapacity;
        mCurrentEnrollmentOrNull = currentEnrollmentOrNull;
    }

    public static CatalogOfferingCapacityMetadata CreateWithoutCurrentEnrollment(OfferingSeatCapacity seatCapacity)
    {
        return new CatalogOfferingCapacityMetadata(seatCapacity, null);
    }

    public static CatalogOfferingCapacityMetadata CreateWithCurrentEnrollment(OfferingSeatCapacity seatCapacity, OfferingEnrollmentCount currentEnrollment)
    {
        return new CatalogOfferingCapacityMetadata(seatCapacity, currentEnrollment);
    }

    public OfferingEnrollmentCount GetCurrentEnrollment()
    {
        if (mCurrentEnrollmentOrNull.HasValue == false)
        {
            throw new InvalidOperationException("No current enrollment value is available.");
        }

        return mCurrentEnrollmentOrNull.Value;
    }
}
