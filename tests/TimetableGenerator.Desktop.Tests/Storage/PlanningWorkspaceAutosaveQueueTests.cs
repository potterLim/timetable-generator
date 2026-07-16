using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Storage;

public sealed class PlanningWorkspaceAutosaveQueueTests
{
    private static readonly TimeSpan TEST_OPERATION_TIMEOUT = TimeSpan.FromSeconds(5.0);

    [Fact]
    public async Task RapidPendingSnapshotsAreCoalescedAndSerializedAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt firstSaveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt latestSaveAttempt = new ControlledSaveAttempt();
        store.EnqueueSaveAttempt(firstSaveAttempt);
        store.EnqueueSaveAttempt(latestSaveAttempt);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);
        PlanningWorkspace firstWorkspace = createWorkspace(new PlanName("첫 상태"));
        PlanningWorkspace supersededWorkspace = createWorkspace(
            new PlanName("대체된 상태"));
        PlanningWorkspace latestWorkspace = createWorkspace(new PlanName("최신 상태"));

        autosaveQueue.RequestSave(firstWorkspace);
        PlanningWorkspace startedFirstWorkspace = await firstSaveAttempt
            .WaitForStartAsync()
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);
        Assert.Same(firstWorkspace, startedFirstWorkspace);

        autosaveQueue.RequestSave(supersededWorkspace);
        autosaveQueue.RequestSave(latestWorkspace);
        firstSaveAttempt.CompleteSuccessfully();

        PlanningWorkspace startedLatestWorkspace = await latestSaveAttempt
            .WaitForStartAsync()
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);
        Assert.Same(latestWorkspace, startedLatestWorkspace);
        latestSaveAttempt.CompleteSuccessfully();

        await autosaveQueue
            .FlushAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);

        IReadOnlyList<PlanningWorkspace> startedWorkspaces = store.StartedWorkspaces;
        Assert.Equal(2, startedWorkspaces.Count);
        Assert.Same(firstWorkspace, startedWorkspaces[0]);
        Assert.Same(latestWorkspace, startedWorkspaces[1]);
        Assert.DoesNotContain(supersededWorkspace, startedWorkspaces);
        Assert.Equal(1, store.MaximumConcurrentSaveCount);
    }

    [Fact]
    public async Task FailedSaveIsReportedAndTheLatestPendingSnapshotStillSavesAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt failedSaveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt successfulSaveAttempt = new ControlledSaveAttempt();
        store.EnqueueSaveAttempt(failedSaveAttempt);
        store.EnqueueSaveAttempt(successfulSaveAttempt);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);
        ConcurrentQueue<PlanningWorkspaceAutosaveState> observedStates =
            new ConcurrentQueue<PlanningWorkspaceAutosaveState>();
        autosaveQueue.StateChanged += delegate (
            object? senderOrNull,
            PlanningWorkspaceAutosaveStateChangedEventArgs eventArguments)
        {
            observedStates.Enqueue(eventArguments.State);
        };
        PlanningWorkspace failedWorkspace = createWorkspace(new PlanName("실패 상태"));
        PlanningWorkspace successfulWorkspace = createWorkspace(
            new PlanName("복구 상태"));
        InvalidOperationException saveFailure =
            new InvalidOperationException("Expected test save failure.");

        autosaveQueue.RequestSave(failedWorkspace);
        await failedSaveAttempt
            .WaitForStartAsync()
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);
        autosaveQueue.RequestSave(successfulWorkspace);
        failedSaveAttempt.CompleteWithFailure(saveFailure);

        PlanningWorkspace startedSuccessfulWorkspace = await successfulSaveAttempt
            .WaitForStartAsync()
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);
        Assert.Same(successfulWorkspace, startedSuccessfulWorkspace);
        successfulSaveAttempt.CompleteSuccessfully();

        await autosaveQueue
            .FlushAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);

        PlanningWorkspaceAutosaveState[] states = observedStates.ToArray();
        Assert.Equal(4, states.Length);

        PlanningWorkspaceAutosaveSavingState firstSavingState =
            Assert.IsType<PlanningWorkspaceAutosaveSavingState>(states[0]);
        Assert.Same(failedWorkspace, firstSavingState.Workspace);
        Assert.Equal(EPlanningWorkspaceAutosaveStatus.Saving, firstSavingState.Status);

        PlanningWorkspaceAutosaveFailedState failedState =
            Assert.IsType<PlanningWorkspaceAutosaveFailedState>(states[1]);
        Assert.Same(failedWorkspace, failedState.Workspace);
        Assert.Same(saveFailure, failedState.Failure);
        Assert.Equal(EPlanningWorkspaceAutosaveStatus.Failed, failedState.Status);

        PlanningWorkspaceAutosaveSavingState secondSavingState =
            Assert.IsType<PlanningWorkspaceAutosaveSavingState>(states[2]);
        Assert.Same(successfulWorkspace, secondSavingState.Workspace);

        PlanningWorkspaceAutosaveSavedState savedState =
            Assert.IsType<PlanningWorkspaceAutosaveSavedState>(states[3]);
        Assert.Same(successfulWorkspace, savedState.Workspace);
        Assert.Equal(EPlanningWorkspaceAutosaveStatus.Saved, savedState.Status);
        Assert.Same(savedState, autosaveQueue.CurrentStateOrNull);
    }

    [Fact]
    public async Task CanceledFlushStopsWaitingWithoutCancelingTheCommittedSaveAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt saveAttempt = new ControlledSaveAttempt();
        store.EnqueueSaveAttempt(saveAttempt);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);
        PlanningWorkspace workspace = createWorkspace(new PlanName("종료 저장 상태"));

        autosaveQueue.RequestSave(workspace);
        await saveAttempt
            .WaitForStartAsync()
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);

        using (CancellationTokenSource flushCancellationSource =
            new CancellationTokenSource())
        {
            Task canceledFlushTask = autosaveQueue.FlushAsync(
                flushCancellationSource.Token);
            flushCancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async delegate
                {
                    await canceledFlushTask;
                });
        }

        CancellationToken saveCancellationToken =
            Assert.Single(store.SaveCancellationTokens);
        Assert.False(saveCancellationToken.CanBeCanceled);

        saveAttempt.CompleteSuccessfully();
        await autosaveQueue
            .FlushAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);

        PlanningWorkspaceAutosaveSavedState savedState =
            Assert.IsType<PlanningWorkspaceAutosaveSavedState>(
                autosaveQueue.CurrentStateOrNull);
        Assert.Same(workspace, savedState.Workspace);
    }

    [Fact]
    public async Task FailingStateSubscriberDoesNotInterruptPersistenceAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt saveAttempt = new ControlledSaveAttempt();
        saveAttempt.CompleteSuccessfully();
        store.EnqueueSaveAttempt(saveAttempt);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);
        ConcurrentQueue<PlanningWorkspaceAutosaveState> observedStates =
            new ConcurrentQueue<PlanningWorkspaceAutosaveState>();
        autosaveQueue.StateChanged += throwStateSubscriberFailure;
        autosaveQueue.StateChanged += delegate (
            object? senderOrNull,
            PlanningWorkspaceAutosaveStateChangedEventArgs eventArguments)
        {
            observedStates.Enqueue(eventArguments.State);
        };

        autosaveQueue.RequestSave(
            createWorkspace(new PlanName("구독자 실패 상태")));
        await autosaveQueue
            .FlushAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TEST_OPERATION_TIMEOUT, TestContext.Current.CancellationToken);

        PlanningWorkspaceAutosaveState[] states = observedStates.ToArray();
        Assert.Equal(2, states.Length);
        Assert.IsType<PlanningWorkspaceAutosaveSavingState>(states[0]);
        Assert.IsType<PlanningWorkspaceAutosaveSavedState>(states[1]);
    }

    [Fact]
    public async Task AlreadyCanceledFlushReturnsACanceledTaskAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);

        using (CancellationTokenSource flushCancellationSource =
            new CancellationTokenSource())
        {
            flushCancellationSource.Cancel();
            Task canceledFlushTask = autosaveQueue.FlushAsync(
                flushCancellationSource.Token);

            Assert.True(canceledFlushTask.IsCanceled);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async delegate
                {
                    await canceledFlushTask;
                });
        }
    }

    [Fact]
    public async Task CompleteReportsTheLastSaveFailureAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt saveAttempt = new ControlledSaveAttempt();
        InvalidOperationException saveFailure =
            new InvalidOperationException("Expected completion failure.");
        saveAttempt.CompleteWithFailure(saveFailure);
        store.EnqueueSaveAttempt(saveAttempt);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);
        autosaveQueue.RequestSave(createWorkspace(new PlanName("실패할 저장")));

        PlanningWorkspaceAutosaveException exception =
            await Assert.ThrowsAsync<PlanningWorkspaceAutosaveException>(
                async delegate
                {
                    await autosaveQueue.CompleteAsync(
                        TestContext.Current.CancellationToken);
                });

        Assert.Same(saveFailure, exception.InnerException);
    }

    [Fact]
    public async Task CompleteSucceedsAfterTheLatestSnapshotIsSavedAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt saveAttempt = new ControlledSaveAttempt();
        saveAttempt.CompleteSuccessfully();
        store.EnqueueSaveAttempt(saveAttempt);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(store);
        autosaveQueue.RequestSave(createWorkspace(new PlanName("저장 완료")));

        await autosaveQueue.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.IsType<PlanningWorkspaceAutosaveSavedState>(
            autosaveQueue.CurrentStateOrNull);
    }

    private static PlanningWorkspace createWorkspace(PlanName planName)
    {
        return PlanningWorkspaceTestFactory.CreateWorkspace(planName);
    }

    private static void throwStateSubscriberFailure(
        object? senderOrNull,
        PlanningWorkspaceAutosaveStateChangedEventArgs eventArguments)
    {
        throw new InvalidOperationException("Expected test subscriber failure.");
    }
}
