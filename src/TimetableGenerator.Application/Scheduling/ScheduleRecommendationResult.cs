using System;
using System.Collections.Generic;

namespace TimetableGenerator.Application.Scheduling;

public sealed class ScheduleRecommendationResult
{
    private readonly IReadOnlyList<ScheduleRecommendation> mRecommendations;

    public IReadOnlyList<ScheduleRecommendation> Recommendations
    {
        get
        {
            return mRecommendations;
        }
    }

    public EScheduleRecommendationCompletion Completion { get; }

    public EPlanCatalogValidationError ValidationError { get; }

    public bool IsSuccessful
    {
        get
        {
            return Completion == EScheduleRecommendationCompletion.Completed || Completion == EScheduleRecommendationCompletion.MaximumRecommendationCountReached;
        }
    }

    public bool HasValidationError
    {
        get
        {
            return ValidationError != EPlanCatalogValidationError.None;
        }
    }

    private ScheduleRecommendationResult(IEnumerable<ScheduleRecommendation> recommendations, EScheduleRecommendationCompletion completion, EPlanCatalogValidationError validationError)
    {
        if (recommendations == null)
        {
            throw new ArgumentNullException(nameof(recommendations));
        }

        if (Enum.IsDefined(typeof(EScheduleRecommendationCompletion), completion) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        if (Enum.IsDefined(typeof(EPlanCatalogValidationError), validationError) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(validationError));
        }

        List<ScheduleRecommendation> copiedRecommendations = new List<ScheduleRecommendation>();
        foreach (ScheduleRecommendation recommendation in recommendations)
        {
            if (recommendation == null)
            {
                throw new ArgumentException("Schedule recommendation results cannot contain null recommendations.", nameof(recommendations));
            }

            copiedRecommendations.Add(recommendation);
        }

        bool isInvalidPlan = completion == EScheduleRecommendationCompletion.InvalidPlan;
        bool hasValidationError = validationError != EPlanCatalogValidationError.None;
        if (isInvalidPlan != hasValidationError)
        {
            throw new ArgumentException("Only invalid plan results can contain plan validation errors.");
        }

        if (isInvalidPlan && copiedRecommendations.Count > 0)
        {
            throw new ArgumentException("Invalid plan results cannot contain schedule recommendations.", nameof(recommendations));
        }

        mRecommendations = copiedRecommendations.AsReadOnly();
        Completion = completion;
        ValidationError = validationError;
    }

    internal static ScheduleRecommendationResult createCompleted(IEnumerable<ScheduleRecommendation> recommendations, EScheduleRecommendationCompletion completion)
    {
        if (completion != EScheduleRecommendationCompletion.Completed && completion != EScheduleRecommendationCompletion.MaximumRecommendationCountReached)
        {
            throw new ArgumentOutOfRangeException(nameof(completion));
        }

        return new ScheduleRecommendationResult(recommendations, completion, EPlanCatalogValidationError.None);
    }

    internal static ScheduleRecommendationResult createCanceled(IEnumerable<ScheduleRecommendation> recommendations)
    {
        return new ScheduleRecommendationResult(recommendations, EScheduleRecommendationCompletion.Canceled, EPlanCatalogValidationError.None);
    }

    internal static ScheduleRecommendationResult createInvalidPlan(EPlanCatalogValidationError validationError)
    {
        if (validationError == EPlanCatalogValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(validationError));
        }

        return new ScheduleRecommendationResult(Array.Empty<ScheduleRecommendation>(), EScheduleRecommendationCompletion.InvalidPlan, validationError);
    }
}
