using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanTabItem : ObservableObject
{
    private static readonly CourseCredits CREDIT_LIMIT = new CourseCredits(19m);

    public PlanId PlanId { get; }

    public PlanName Name { get; }

    public string DisplayName
    {
        get
        {
            return Name.Value;
        }
    }

    public ObservableCollection<PlanCourseItem> ScheduledCourses { get; }

    public ObservableCollection<PlanCourseItem> UnconfirmedCourses { get; }

    public int SelectedCourseCount
    {
        get
        {
            return ScheduledCourses.Count;
        }
    }

    public string ScheduledCourseHeading
    {
        get
        {
            return "수강 과목 (" + SelectedCourseCount + ")";
        }
    }

    public decimal TotalCredits
    {
        get
        {
            return findTotalCredits().Value;
        }
    }

    public decimal CreditLimit
    {
        get
        {
            return CREDIT_LIMIT.Value;
        }
    }

    public string CreditSummary
    {
        get
        {
            CourseCredits totalCredits = findTotalCredits();
            return totalCredits + " / " + CREDIT_LIMIT + "학점";
        }
    }

    public string UnconfirmedHeading
    {
        get
        {
            return "시간 미정 " + UnconfirmedCourses.Count;
        }
    }

    public PlanTabItem(
        PlanId planId,
        PlanName name,
        ObservableCollection<PlanCourseItem> scheduledCourses,
        ObservableCollection<PlanCourseItem> unconfirmedCourses)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException("Plan IDs must be valid.", nameof(planId));
        }

        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(scheduledCourses);
        ArgumentNullException.ThrowIfNull(unconfirmedCourses);

        PlanId = planId;
        Name = name;
        ScheduledCourses = scheduledCourses;
        UnconfirmedCourses = unconfirmedCourses;

        ScheduledCourses.CollectionChanged += onCourseCollectionChanged;
        UnconfirmedCourses.CollectionChanged += onCourseCollectionChanged;
    }

    public bool ContainsCourse(CourseId courseId)
    {
        return findCourseOrNull(ScheduledCourses, courseId) != null ||
            findCourseOrNull(UnconfirmedCourses, courseId) != null;
    }

    public void AddCourse(PlanCourseItem course)
    {
        ArgumentNullException.ThrowIfNull(course);
        if (ContainsCourse(course.CourseId))
        {
            return;
        }

        if (course.HasConfirmedSchedule)
        {
            ScheduledCourses.Add(course);
        }
        else
        {
            UnconfirmedCourses.Add(course);
        }
    }

    public void RemoveCourse(PlanCourseItem course)
    {
        ArgumentNullException.ThrowIfNull(course);
        if (ScheduledCourses.Remove(course))
        {
            return;
        }

        UnconfirmedCourses.Remove(course);
    }

    private static PlanCourseItem? findCourseOrNull(
        ObservableCollection<PlanCourseItem> courses,
        CourseId courseId)
    {
        foreach (PlanCourseItem course in courses)
        {
            if (course.CourseId == courseId)
            {
                return course;
            }
        }

        return null;
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

    private void onCourseCollectionChanged(
        object? senderOrNull,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        raisePropertyChanged(nameof(SelectedCourseCount));
        raisePropertyChanged(nameof(ScheduledCourseHeading));
        raisePropertyChanged(nameof(TotalCredits));
        raisePropertyChanged(nameof(CreditSummary));
        raisePropertyChanged(nameof(UnconfirmedHeading));
    }
}
