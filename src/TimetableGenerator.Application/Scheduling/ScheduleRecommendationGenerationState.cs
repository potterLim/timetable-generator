using System;
using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ScheduleRecommendationGenerationState
{
    private readonly IReadOnlyList<RecommendationScore> mRemainingMinimumScores;

    private readonly PriorityQueue<ScheduleSearchNode, ScheduleSearchPriority> mPendingNodes;

    private long mNextSequence;

    public IReadOnlyList<ValidatedCourseChoiceGroup> CourseChoiceGroups { get; }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledSelections { get; }

    public IReadOnlyList<PersonalSchedule> PersonalSchedules { get; }

    public ScheduleRecommendationLimit MaximumRecommendationCount { get; }

    public CancellationToken CancellationToken { get; }

    public List<ScheduleRecommendation> Recommendations { get; }

    public EScheduleRecommendationCompletion Completion { get; private set; }

    public bool ShouldStop
    {
        get
        {
            return Completion != EScheduleRecommendationCompletion.Completed;
        }
    }

    public ScheduleRecommendationGenerationState(
        IReadOnlyList<ValidatedCourseChoiceGroup> courseChoiceGroups,
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections,
        IReadOnlyList<PersonalSchedule> personalSchedules,
        ScheduleRecommendationLimit maximumRecommendationCount,
        CancellationToken cancellationToken)
    {
        if (courseChoiceGroups == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroups));
        }

        if (unscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledSelections));
        }

        if (personalSchedules == null)
        {
            throw new ArgumentNullException(nameof(personalSchedules));
        }

        if (maximumRecommendationCount.IsValid == false)
        {
            throw new ArgumentException(
                "Recommendation generation state requires a valid result limit.",
                nameof(maximumRecommendationCount));
        }

        CourseChoiceGroups = courseChoiceGroups;
        UnscheduledSelections = unscheduledSelections;
        PersonalSchedules = personalSchedules;
        MaximumRecommendationCount = maximumRecommendationCount;
        CancellationToken = cancellationToken;
        Recommendations = new List<ScheduleRecommendation>();
        Completion = EScheduleRecommendationCompletion.Completed;
        mRemainingMinimumScores = createRemainingMinimumScores(courseChoiceGroups);
        mPendingNodes = new PriorityQueue<ScheduleSearchNode, ScheduleSearchPriority>();
        mNextSequence = 0L;
        EnqueueNode(ScheduleSearchNode.CreateRoot());
    }

    public ScheduleSearchNode DequeueNode()
    {
        return mPendingNodes.Dequeue();
    }

    public bool HasPendingNodes()
    {
        return mPendingNodes.Count > 0;
    }

    public void EnqueueNode(ScheduleSearchNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        RecommendationScore remainingMinimumScore = mRemainingMinimumScores[node.NextGroupIndex];
        RecommendationScore optimisticScore = node.Score.Add(remainingMinimumScore);
        int remainingGroupCount = CourseChoiceGroups.Count - node.NextGroupIndex;
        ScheduleSearchPriority priority = new ScheduleSearchPriority(
            optimisticScore,
            remainingGroupCount,
            mNextSequence);
        mNextSequence = checked(mNextSequence + 1L);
        mPendingNodes.Enqueue(node, priority);
    }

    public void MarkCanceled()
    {
        Completion = EScheduleRecommendationCompletion.Canceled;
    }

    public void MarkMaximumRecommendationCountReached()
    {
        Completion = EScheduleRecommendationCompletion.MaximumRecommendationCountReached;
    }

    public IReadOnlyList<UnscheduledOfferingSelection> CombineUnscheduledSelections(
            IReadOnlyList<UnscheduledOfferingSelection> selectedByGroups)
    {
        if (selectedByGroups == null)
        {
            throw new ArgumentNullException(nameof(selectedByGroups));
        }

        List<UnscheduledOfferingSelection> combinedSelections =
            new List<UnscheduledOfferingSelection>(
                selectedByGroups.Count + UnscheduledSelections.Count);
        combinedSelections.AddRange(selectedByGroups);
        combinedSelections.AddRange(UnscheduledSelections);
        return combinedSelections.AsReadOnly();
    }

    private static IReadOnlyList<RecommendationScore> createRemainingMinimumScores(
        IReadOnlyList<ValidatedCourseChoiceGroup> courseChoiceGroups)
    {
        RecommendationScore[] remainingMinimumScores = new RecommendationScore[courseChoiceGroups.Count + 1];
        remainingMinimumScores[courseChoiceGroups.Count] = RecommendationScore.ZERO;
        for (int groupIndex = courseChoiceGroups.Count - 1;
            groupIndex >= 0;
            --groupIndex)
        {
            remainingMinimumScores[groupIndex] =
                courseChoiceGroups[groupIndex].MinimumScore.Add(
                    remainingMinimumScores[groupIndex + 1]);
        }

        return Array.AsReadOnly(remainingMinimumScores);
    }
}
