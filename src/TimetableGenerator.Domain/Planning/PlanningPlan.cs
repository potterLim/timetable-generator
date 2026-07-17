using System;
using System.Collections.Generic;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningPlan
{
    public PlanId Id { get; }

    public PlanName Name { get; }

    public PlanCatalogBinding CatalogBinding { get; }

    public PlanningPlanContent Content { get; }

    public IReadOnlyList<CourseChoiceGroup> CourseChoiceGroups
    {
        get
        {
            return Content.CourseChoiceGroups;
        }
    }

    public IReadOnlyList<ScheduledCourseChoice> ScheduledCourseChoices
    {
        get
        {
            return Content.ScheduledCourseChoices;
        }
    }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledOfferingSelections
    {
        get
        {
            return Content.UnscheduledOfferingSelections;
        }
    }

    public IReadOnlyList<PersonalSchedule> PersonalSchedules
    {
        get
        {
            return Content.PersonalSchedules;
        }
    }

    public bool HasUnscheduledOfferingSelections
    {
        get
        {
            return UnscheduledOfferingSelections.Count > 0;
        }
    }

    public PlanningPlan(
        PlanId id,
        PlanName name,
        PlanCatalogBinding catalogBinding,
        PlanningPlanContent content)
    {
        if (id.IsValid == false)
        {
            throw new ArgumentException("Planning plans require a valid ID.", nameof(id));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (catalogBinding == null)
        {
            throw new ArgumentNullException(nameof(catalogBinding));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        Id = id;
        Name = name;
        CatalogBinding = catalogBinding;
        Content = content;
    }
}
