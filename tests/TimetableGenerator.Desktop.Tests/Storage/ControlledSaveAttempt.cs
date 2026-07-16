using System;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Storage;

internal sealed class ControlledSaveAttempt
{
    private readonly TaskCompletionSource<PlanningWorkspace> mStartedCompletionSource;
    private readonly TaskCompletionSource mSaveCompletionSource;

    public ControlledSaveAttempt()
    {
        mStartedCompletionSource = new TaskCompletionSource<PlanningWorkspace>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        mSaveCompletionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task<PlanningWorkspace> WaitForStartAsync()
    {
        return mStartedCompletionSource.Task;
    }

    public void CompleteSuccessfully()
    {
        mSaveCompletionSource.SetResult();
    }

    public void CompleteWithFailure(Exception failure)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        mSaveCompletionSource.SetException(failure);
    }

    internal void markStarted(PlanningWorkspace workspace)
    {
        mStartedCompletionSource.SetResult(workspace);
    }

    internal Task waitForCompletionAsync()
    {
        return mSaveCompletionSource.Task;
    }
}
