using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

using TimetableGenerator.Desktop.Presentation;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanTabItem : ObservableObject
{
    private const int CREDIT_LIMIT = 19;

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

    public int TotalCredits
    {
        get
        {
            int totalCredits = 0;
            foreach (PlanCourseItem course in ScheduledCourses)
            {
                totalCredits += course.Credits.Value;
            }

            foreach (PlanCourseItem course in UnconfirmedCourses)
            {
                totalCredits += course.Credits.Value;
            }

            return totalCredits;
        }
    }

    public int CreditLimit
    {
        get
        {
            return CREDIT_LIMIT;
        }
    }

    public string CreditSummary
    {
        get
        {
            return TotalCredits + " / " + CreditLimit + "학점";
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
