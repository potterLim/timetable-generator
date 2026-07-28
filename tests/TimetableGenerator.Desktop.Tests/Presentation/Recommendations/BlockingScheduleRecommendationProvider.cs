using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Presentation.Recommendations;

internal sealed class BlockingScheduleRecommendationProvider :
    IScheduleRecommendationProvider
{
    private readonly TaskCompletionSource<bool> mFirstCallStartedSource;

    private readonly TaskCompletionSource<bool> mFirstCallCanceledSource;

    private readonly TaskCompletionSource<bool> mSecondCallStartedSource;

    private int mCallCount;

    public Task FirstCallStarted
    {
        get
        {
            return mFirstCallStartedSource.Task;
        }
    }

    public Task FirstCallCanceled
    {
        get
        {
            return mFirstCallCanceledSource.Task;
        }
    }

    public Task SecondCallStarted
    {
        get
        {
            return mSecondCallStartedSource.Task;
        }
    }

    public BlockingScheduleRecommendationProvider()
    {
        mFirstCallStartedSource = createCompletionSource();
        mFirstCallCanceledSource = createCompletionSource();
        mSecondCallStartedSource = createCompletionSource();
    }

    public ScheduleRecommendationResult Generate(PlanningPlan plan, ScheduleRecommendationLimit recommendationLimit, CancellationToken cancellationToken)
    {
        int callNumber = Interlocked.Increment(ref mCallCount);
        if (callNumber == 1)
        {
            mFirstCallStartedSource.TrySetResult(true);
        }
        else if (callNumber == 2)
        {
            mSecondCallStartedSource.TrySetResult(true);
        }

        try
        {
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new System.InvalidOperationException("The blocking recommendation provider resumed without cancellation.");
        }
        catch (OperationCanceledException)
            when (callNumber == 1)
        {
            mFirstCallCanceledSource.TrySetResult(true);
            throw;
        }
    }

    private static TaskCompletionSource<bool> createCompletionSource()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
