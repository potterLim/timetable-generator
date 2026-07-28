using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct AcademicTermDateRange
{
    private readonly bool mIsInitialized;

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    public bool IsValid
    {
        get
        {
            return mIsInitialized && StartDate <= EndDate;
        }
    }

    public AcademicTermDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("The academic term end date cannot precede its start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
        mIsInitialized = true;
    }
}
