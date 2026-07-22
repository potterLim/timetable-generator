using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanTabItem
{
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
            return DisplayName + " 시간표 삭제";
        }
    }

    public string CloseButtonHelpText
    {
        get
        {
            return "시간표 삭제";
        }
    }

    public bool CanClose
    {
        get
        {
            return true;
        }
    }

    public ICommand CloseCommand { get; }

    public ICommand RenameCommand { get; }

    public ObservableCollection<PlanCourseChoiceGroupItem> CourseChoiceGroups { get; }

    public ObservableCollection<TimeNotProvidedCourseItem> TimeNotProvidedCourses
    {
        get;
    }

    public ObservableCollection<PersonalScheduleItem> PersonalSchedules { get; }

    public int SelectedCourseCount
    {
        get
        {
            return CourseChoiceGroups.Count + TimeNotProvidedCourses.Count;
        }
    }

    public bool HasSelectedCourses
    {
        get
        {
            return SelectedCourseCount > 0;
        }
    }

    public bool HasPersonalSchedules
    {
        get
        {
            return PersonalSchedules.Count > 0;
        }
    }

    public bool IsCompletelyEmpty
    {
        get
        {
            return HasSelectedCourses == false && HasPersonalSchedules == false;
        }
    }

    public bool HasOnlyPersonalSchedules
    {
        get
        {
            return HasSelectedCourses == false && HasPersonalSchedules;
        }
    }

    public string PersonalScheduleHeading
    {
        get
        {
            return "개인 일정 (" + PersonalSchedules.Count + ")";
        }
    }

    public bool HasScheduledCourses
    {
        get
        {
            return CourseChoiceGroups.Count > 0;
        }
    }

    public string ScheduledCourseHeading
    {
        get
        {
            return "수강 선택 (" + CourseChoiceGroups.Count + ")";
        }
    }

    public string ExpectedCreditsDisplayText
    {
        get
        {
            CourseCredits minimumCredits = findMinimumCredits();
            CourseCredits maximumCredits = findMaximumCredits();
            string creditText = minimumCredits.ToString();
            if (minimumCredits != maximumCredits)
            {
                creditText += "–" + maximumCredits;
            }

            return creditText + "학점";
        }
    }

    public string ExpectedCreditsAccessibleName
    {
        get
        {
            return "예상 학점 " + ExpectedCreditsDisplayText;
        }
    }

    public string TimeNotProvidedHeading
    {
        get
        {
            return "시간 미정 " + TimeNotProvidedCourses.Count;
        }
    }

    public bool HasTimeNotProvidedCourses
    {
        get
        {
            return TimeNotProvidedCourses.Count > 0;
        }
    }

    public PlanTabItem(
        PlanningPlan plan,
        CourseCatalogProjection catalogProjection,
        Action<PlanTabItem> requestRenamePlan,
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

        if (requestClosePlan == null)
        {
            throw new ArgumentNullException(nameof(requestClosePlan));
        }

        if (requestRenamePlan == null)
        {
            throw new ArgumentNullException(nameof(requestRenamePlan));
        }

        Plan = plan;
        RenameCommand = new DelegateCommand(
            delegate
            {
                requestRenamePlan(this);
            });
        CloseCommand = new DelegateCommand(
            delegate
            {
                requestClosePlan(this);
            },
            canClose);
        CourseChoiceGroups = new ObservableCollection<PlanCourseChoiceGroupItem>();
        TimeNotProvidedCourses = new ObservableCollection<TimeNotProvidedCourseItem>();
        PersonalSchedules = new ObservableCollection<PersonalScheduleItem>();
        foreach (CourseChoiceGroup group in plan.CourseChoiceGroups)
        {
            CourseChoiceGroups.Add(new PlanCourseChoiceGroupItem(group, catalogProjection));
        }

        foreach (UnscheduledOfferingSelection selection
            in plan.UnscheduledOfferingSelections)
        {
            CatalogCourseProjection course = catalogProjection.FindCourseById(selection.CourseId);
            TimeNotProvidedCourses.Add(new TimeNotProvidedCourseItem(course, selection));
        }

        foreach (PersonalSchedule personalSchedule in plan.PersonalSchedules)
        {
            PersonalSchedules.Add(new PersonalScheduleItem(personalSchedule));
        }
    }

    public bool ContainsCourse(CourseId courseId)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        foreach (CourseChoiceGroup group in Plan.CourseChoiceGroups)
        {
            foreach (CourseCandidate candidate in group.CourseCandidates)
            {
                if (candidate.CourseId == courseId)
                {
                    return true;
                }
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

    private bool canClose()
    {
        return CanClose;
    }

    private CourseCredits findMinimumCredits()
    {
        decimal totalCreditValue = 0m;
        foreach (PlanCourseChoiceGroupItem group in CourseChoiceGroups)
        {
            totalCreditValue += group.MinimumCredits.Value;
        }

        foreach (TimeNotProvidedCourseItem course in TimeNotProvidedCourses)
        {
            totalCreditValue += course.Credits.Value;
        }

        return new CourseCredits(totalCreditValue);
    }

    private CourseCredits findMaximumCredits()
    {
        decimal totalCreditValue = 0m;
        foreach (PlanCourseChoiceGroupItem group in CourseChoiceGroups)
        {
            totalCreditValue += group.MaximumCredits.Value;
        }

        foreach (TimeNotProvidedCourseItem course in TimeNotProvidedCourses)
        {
            totalCreditValue += course.Credits.Value;
        }

        return new CourseCredits(totalCreditValue);
    }
}
