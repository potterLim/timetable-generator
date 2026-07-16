using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanTabItem
{
    private readonly EPlanCloseAvailability mCloseAvailability;

    public PlanningPlan Plan { get; }

    public PlanId PlanId
    {
        get
        {
            return Plan.Id;
        }
    }

    public PlanName Name
    {
        get
        {
            return Plan.Name;
        }
    }

    public string DisplayName
    {
        get
        {
            return Name.Value;
        }
    }

    public string CloseButtonAccessibleName
    {
        get
        {
            return DisplayName + " 계획 닫기";
        }
    }

    public bool CanClose
    {
        get
        {
            return mCloseAvailability == EPlanCloseAvailability.Available;
        }
    }

    public ICommand CloseCommand { get; }

    public ObservableCollection<PlanCourseItem> ScheduledCourses { get; }

    public ObservableCollection<PlanCourseItem> UnconfirmedCourses { get; }

    public int SelectedCourseCount
    {
        get
        {
            return ScheduledCourses.Count + UnconfirmedCourses.Count;
        }
    }

    public string ScheduledCourseHeading
    {
        get
        {
            return "시간표 과목 (" + ScheduledCourses.Count + ")";
        }
    }

    public string CreditSummary
    {
        get
        {
            CourseCredits totalCredits = findTotalCredits();
            return totalCredits + "학점 · " + SelectedCourseCount + "과목";
        }
    }

    public string UnconfirmedHeading
    {
        get
        {
            return "시간 미정 " + UnconfirmedCourses.Count;
        }
    }

    public bool HasUnconfirmedCourses
    {
        get
        {
            return UnconfirmedCourses.Count > 0;
        }
    }

    public PlanTabItem(
        PlanningPlan plan,
        CourseCatalogProjection catalogProjection,
        EPlanCloseAvailability closeAvailability,
        Action<PlanTabItem> requestClosePlan)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (catalogProjection == null)
        {
            throw new ArgumentNullException(nameof(catalogProjection));
        }

        if (Enum.IsDefined(typeof(EPlanCloseAvailability), closeAvailability) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(closeAvailability));
        }

        if (requestClosePlan == null)
        {
            throw new ArgumentNullException(nameof(requestClosePlan));
        }

        Plan = plan;
        mCloseAvailability = closeAvailability;
        CloseCommand = new DelegateCommand(
            delegate
            {
                requestClosePlan(this);
            },
            canClose);
        ScheduledCourses = new ObservableCollection<PlanCourseItem>();
        UnconfirmedCourses = new ObservableCollection<PlanCourseItem>();
        foreach (ScheduledCourseChoice choice in plan.ScheduledCourseChoices)
        {
            CatalogCourseProjection course = catalogProjection.FindCourseById(
                choice.CourseId);
            ScheduledCourses.Add(PlanCourseItem.CreateScheduled(course, choice));
        }

        foreach (UnscheduledOfferingSelection selection
            in plan.UnscheduledOfferingSelections)
        {
            CatalogCourseProjection course = catalogProjection.FindCourseById(
                selection.CourseId);
            UnconfirmedCourses.Add(
                PlanCourseItem.CreateTimeNotProvided(course, selection));
        }
    }

    private bool canClose()
    {
        return CanClose;
    }

    public bool ContainsCourse(CourseId courseId)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        foreach (ScheduledCourseChoice choice in Plan.ScheduledCourseChoices)
        {
            if (choice.CourseId == courseId)
            {
                return true;
            }
        }

        foreach (UnscheduledOfferingSelection selection
            in Plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId == courseId)
            {
                return true;
            }
        }

        return false;
    }

    private CourseCredits findTotalCredits()
    {
        decimal totalCreditValue = 0m;
        foreach (PlanCourseItem course in ScheduledCourses)
        {
            totalCreditValue += course.Credits.Value;
        }

        foreach (PlanCourseItem course in UnconfirmedCourses)
        {
            totalCreditValue += course.Credits.Value;
        }

        return new CourseCredits(totalCreditValue);
    }
}
