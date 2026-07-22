using System;
using System.Collections.ObjectModel;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanCourseChoiceGroupItem
{
    public CourseChoiceGroup Group { get; }

    public CourseChoiceGroupId GroupId
    {
        get
        {
            return Group.Id;
        }
    }

    public ObservableCollection<PlanCourseChoiceCandidateItem> Courses { get; }

    public bool IsAlternativeGroup
    {
        get
        {
            return Courses.Count > 1;
        }
    }

    public bool IsSingleCourseGroup
    {
        get
        {
            return Courses.Count == 1;
        }
    }

    public PlanCourseChoiceCandidateItem SingleCourse
    {
        get
        {
            if (IsSingleCourseGroup == false)
            {
                throw new InvalidOperationException("Only singleton course choice groups have a single course.");
            }

            return Courses[0];
        }
    }

    public string Heading
    {
        get
        {
            if (IsAlternativeGroup)
            {
                return Courses.Count + "개 과목 중 1개 선택";
            }

            return "수강 과목";
        }
    }

    public string AccessibleName
    {
        get
        {
            if (IsAlternativeGroup)
            {
                return Courses.Count + "개 과목 중 1개를 선택하는 수강 선택";
            }

            return SingleCourse.Name + " 수강 선택";
        }
    }

    public string EditButtonAccessibleName
    {
        get
        {
            if (IsSingleCourseGroup)
            {
                return SingleCourse.Name + " 수강 설정 수정";
            }

            return "대안 과목 수강 선택 수정";
        }
    }

    public string RemoveButtonAccessibleName
    {
        get
        {
            if (IsSingleCourseGroup)
            {
                return SingleCourse.Name + " 시간표에서 제거";
            }

            return "대안 과목 수강 선택을 시간표에서 제거";
        }
    }

    public CourseCredits MinimumCredits { get; }

    public CourseCredits MaximumCredits { get; }

    public PlanCourseChoiceGroupItem(CourseChoiceGroup group, CourseCatalogProjection catalogProjection)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        if (catalogProjection == null)
        {
            throw new ArgumentNullException(nameof(catalogProjection));
        }

        Group = group;
        Courses = new ObservableCollection<PlanCourseChoiceCandidateItem>();
        decimal minimumCreditValue = decimal.MaxValue;
        decimal maximumCreditValue = decimal.MinValue;
        foreach (CourseCandidate courseCandidate in group.CourseCandidates)
        {
            CatalogCourseProjection projection = catalogProjection.FindCourseById(courseCandidate.CourseId);
            PlanCourseChoiceCandidateItem item = new PlanCourseChoiceCandidateItem(projection, courseCandidate);
            Courses.Add(item);
            minimumCreditValue = Math.Min(minimumCreditValue, item.Credits.Value);
            maximumCreditValue = Math.Max(maximumCreditValue, item.Credits.Value);
        }

        if (Courses.Count == 0)
        {
            throw new ArgumentException(
                "Plan course choice groups require at least one course.",
                nameof(group));
        }

        MinimumCredits = new CourseCredits(minimumCreditValue);
        MaximumCredits = new CourseCredits(maximumCreditValue);
    }

    public void SynchronizeSelectedOfferings(
        ScheduleRecommendationBookmark? recommendationBookmarkOrNull)
    {
        foreach (PlanCourseChoiceCandidateItem course in Courses)
        {
            course.SynchronizeSelectedOffering(recommendationBookmarkOrNull);
        }
    }
}
