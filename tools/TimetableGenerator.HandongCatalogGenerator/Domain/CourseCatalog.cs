using System;
using System.Collections.Generic;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class CourseCatalog
{
    private readonly IReadOnlyList<CatalogCourse> mCourses;
    private readonly IReadOnlyList<CatalogOffering> mOfferings;

    public IReadOnlyList<CatalogCourse> Courses
    {
        get
        {
            return mCourses;
        }
    }

    public IReadOnlyList<CatalogOffering> Offerings
    {
        get
        {
            return mOfferings;
        }
    }

    public CatalogDataQuality DataQuality { get; }

    public CatalogItemCount CourseCount
    {
        get
        {
            return new CatalogItemCount(mCourses.Count);
        }
    }

    public CatalogItemCount OfferingCount
    {
        get
        {
            return new CatalogItemCount(mOfferings.Count);
        }
    }

    public CatalogItemCount ScheduledOfferingCount
    {
        get
        {
            int scheduledOfferingCount = 0;
            foreach (CatalogOffering offering in mOfferings)
            {
                if (offering.Logistics.Schedule.Status == EMeetingScheduleStatus.Scheduled)
                {
                    ++scheduledOfferingCount;
                }
            }

            return new CatalogItemCount(scheduledOfferingCount);
        }
    }

    public CatalogItemCount MeetingNotProvidedCount
    {
        get
        {
            return new CatalogItemCount(mOfferings.Count - ScheduledOfferingCount.Value);
        }
    }

    public CourseCatalog(
        IEnumerable<CatalogCourse> courses,
        IEnumerable<CatalogOffering> offerings,
        CatalogDataQuality dataQuality)
    {
        if (courses == null)
        {
            throw new ArgumentNullException(nameof(courses));
        }

        if (offerings == null)
        {
            throw new ArgumentNullException(nameof(offerings));
        }

        if (dataQuality == null)
        {
            throw new ArgumentNullException(nameof(dataQuality));
        }

        List<CatalogCourse> copiedCourses = new List<CatalogCourse>();
        foreach (CatalogCourse course in courses)
        {
            if (course == null)
            {
                throw new ArgumentException("Catalogs cannot contain null courses.", nameof(courses));
            }

            copiedCourses.Add(course);
        }

        List<CatalogOffering> copiedOfferings = new List<CatalogOffering>();
        foreach (CatalogOffering offering in offerings)
        {
            if (offering == null)
            {
                throw new ArgumentException("Catalogs cannot contain null offerings.", nameof(offerings));
            }

            copiedOfferings.Add(offering);
        }

        if (copiedCourses.Count == 0 || copiedOfferings.Count == 0)
        {
            throw new ArgumentException("Course catalogs require courses and offerings.");
        }

        mCourses = copiedCourses.AsReadOnly();
        mOfferings = copiedOfferings.AsReadOnly();
        DataQuality = dataQuality;
    }
}
