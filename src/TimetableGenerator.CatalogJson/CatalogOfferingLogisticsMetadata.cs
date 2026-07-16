using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogOfferingLogisticsMetadata
{
    private readonly KoreanScheduleSourceText? mScheduleSourceTextOrNull;

    public LocationAssignmentMetadata Location { get; }

    public bool HasScheduleSourceText
    {
        get
        {
            return mScheduleSourceTextOrNull != null;
        }
    }

    private CatalogOfferingLogisticsMetadata(
        KoreanScheduleSourceText? scheduleSourceTextOrNull,
        LocationAssignmentMetadata location)
    {
        if (location == null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        mScheduleSourceTextOrNull = scheduleSourceTextOrNull;
        Location = location;
    }

    public static CatalogOfferingLogisticsMetadata CreateScheduled(
        KoreanScheduleSourceText scheduleSourceText,
        LocationAssignmentMetadata location)
    {
        if (scheduleSourceText == null)
        {
            throw new ArgumentNullException(nameof(scheduleSourceText));
        }

        return new CatalogOfferingLogisticsMetadata(scheduleSourceText, location);
    }

    public static CatalogOfferingLogisticsMetadata CreateWithoutProvidedSchedule(
        LocationAssignmentMetadata location)
    {
        return new CatalogOfferingLogisticsMetadata(null, location);
    }

    public KoreanScheduleSourceText GetScheduleSourceText()
    {
        if (mScheduleSourceTextOrNull == null)
        {
            throw new InvalidOperationException("No Korean schedule source text is available.");
        }

        return mScheduleSourceTextOrNull;
    }
}
