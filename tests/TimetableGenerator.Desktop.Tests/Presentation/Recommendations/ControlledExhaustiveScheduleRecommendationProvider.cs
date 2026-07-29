using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Presentation.Recommendations;

internal enum EControlledExhaustiveOutcome
{
    WaitForCancellation = 0,
    ThrowException = 1,
}

internal sealed class ControlledExhaustiveScheduleRecommendationProvider :
    IScheduleRecommendationProvider
{
    private readonly CatalogScheduleRecommendationProvider mInnerProvider;

    private readonly EControlledExhaustiveOutcome mExhaustiveOutcome;

    private readonly TaskCompletionSource<bool> mExhaustiveCallStartedSource;

    private readonly TaskCompletionSource<bool> mExhaustiveCallCanceledSource;

    private int mCallCount;

    public Task ExhaustiveCallStarted
    {
        get
        {
            return mExhaustiveCallStartedSource.Task;
        }
    }

    public Task ExhaustiveCallCanceled
    {
        get
        {
            return mExhaustiveCallCanceledSource.Task;
        }
    }

    public ControlledExhaustiveScheduleRecommendationProvider(
        CourseCatalog catalog,
        EControlledExhaustiveOutcome exhaustiveOutcome)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (Enum.IsDefined(exhaustiveOutcome) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(exhaustiveOutcome), exhaustiveOutcome, "The controlled exhaustive outcome is not supported.");
        }

        mInnerProvider = new CatalogScheduleRecommendationProvider(catalog);
        mExhaustiveOutcome = exhaustiveOutcome;
        mExhaustiveCallStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        mExhaustiveCallCanceledSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public ScheduleRecommendationResult Generate(
        PlanningPlan plan,
        ScheduleRecommendationLimit recommendationLimit,
        CancellationToken cancellationToken)
    {
        int callNumber = Interlocked.Increment(ref mCallCount);
        if (callNumber == 1)
        {
            return mInnerProvider.Generate(
                plan,
                recommendationLimit,
                cancellationToken);
        }

        if (recommendationLimit.IsUnlimited == false)
        {
            throw new InvalidOperationException(
                "The controlled exhaustive provider expected an unlimited calculation.");
        }

        mExhaustiveCallStartedSource.TrySetResult(true);
        if (mExhaustiveOutcome == EControlledExhaustiveOutcome.ThrowException)
        {
            throw new InvalidOperationException("The controlled exhaustive calculation failed.");
        }

        try
        {
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                "The controlled exhaustive provider resumed without cancellation.");
        }
        catch (OperationCanceledException)
        {
            mExhaustiveCallCanceledSource.TrySetResult(true);
            throw;
        }
    }
}
