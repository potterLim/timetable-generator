using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Headless.XUnit;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Tests.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class AdaptiveRecommendationCalculationTests
{
    private static readonly TimeSpan TEST_TIMEOUT = TimeSpan.FromSeconds(5.0);

    [AvaloniaFact]
    public async Task FastExhaustiveCalculationReplacesTheInitialLimitedResultAsync()
    {
        RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(1), TEST_TIMEOUT);
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(policy))
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            Assert.Equal("1 / 2", workspace.RecommendationSummary);
            Assert.False(workspace.HasAdditionalRecommendations);
            Assert.False(workspace.CanCalculateAllRecommendations);
            Assert.False(workspace.IsCalculatingAllRecommendations);
            Assert.True(workspace.CanExportAllPngCandidates);
        }
    }

    [AvaloniaFact]
    public async Task AutomaticBudgetAlsoCoversCompletedResultProjectionAsync()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        AutomaticBudgetIgnoringScheduleRecommendationProvider recommendationProvider = new AutomaticBudgetIgnoringScheduleRecommendationProvider(document.Catalog);
        RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(1), TimeSpan.FromMilliseconds(20.0));
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(document, recommendationProvider, policy))
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            Assert.Equal("1 / 1+", workspace.RecommendationSummary);
            Assert.True(workspace.HasAdditionalRecommendations);
            Assert.True(workspace.CanCalculateAllRecommendations);
            Assert.False(workspace.CanExportAllPngCandidates);
        }
    }

    [AvaloniaFact]
    public async Task SlowCalculationKeepsTheInitialResultsUntilTheUserRequestsAllAsync()
    {
        RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(1), TimeSpan.Zero);
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(policy))
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            Assert.Equal("1 / 1+", workspace.RecommendationSummary);
            Assert.True(workspace.HasAdditionalRecommendations);
            Assert.True(workspace.CanCalculateAllRecommendations);
            Assert.Equal("가능한 시간표가 많습니다", workspace.AdditionalRecommendationTitle);
            Assert.Equal("먼저 1개를 표시합니다. 전체 계산은 시간이 걸릴 수 있습니다.", workspace.AdditionalRecommendationMessage);
            Assert.Equal("전체 시간표 계산", workspace.CalculateAllRecommendationsActionText);
            Assert.False(workspace.CanExportAllPngCandidates);

            workspace.CalculateAllRecommendationsCommand.Execute(null);
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            Assert.Equal("1 / 2", workspace.RecommendationSummary);
            Assert.False(workspace.HasAdditionalRecommendations);
            Assert.True(workspace.CanExportAllPngCandidates);
        }
    }

    [AvaloniaFact]
    public async Task ManualExhaustiveCalculationRestoresTheRecommendationViewedWhileItRunsAsync()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        using (PausedUnlimitedScheduleRecommendationProvider recommendationProvider = new PausedUnlimitedScheduleRecommendationProvider(document.Catalog))
        {
            RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(2), TimeSpan.Zero);
            using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(document, recommendationProvider, policy))
            {
                await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

                CourseSearchItem seminarCourse = findCourse(workspace, "BFT30009");
                workspace.AddCourseCommand.Execute(seminarCourse);
                workspace.SaveCourseChoiceCommand.Execute(null);
                await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

                Assert.Equal("1 / 2+", workspace.RecommendationSummary);
                workspace.CalculateAllRecommendationsCommand.Execute(null);
                await recommendationProvider.ExhaustiveCallStarted.WaitAsync(TEST_TIMEOUT);

                workspace.NextRecommendationCommand.Execute(null);
                Assert.Equal("2 / 2+", workspace.RecommendationSummary);
                IReadOnlyList<string> expectedOfferingIds = getActiveOfferingIds(workspace);

                recommendationProvider.CompleteExhaustiveCalculation();
                await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

                Assert.Equal("2 / 4", workspace.RecommendationSummary);
                Assert.Equal(
                    expectedOfferingIds,
                    getActiveOfferingIds(workspace));
            }
        }
    }

    [AvaloniaFact]
    public async Task CancelingAnExhaustiveCalculationKeepsTheUsableInitialResultAsync()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        ControlledExhaustiveScheduleRecommendationProvider recommendationProvider = new ControlledExhaustiveScheduleRecommendationProvider(document.Catalog, EControlledExhaustiveOutcome.WaitForCancellation);
        RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(1), TimeSpan.Zero);
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(document, recommendationProvider, policy))
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            workspace.CalculateAllRecommendationsCommand.Execute(null);
            await recommendationProvider.ExhaustiveCallStarted.WaitAsync(TEST_TIMEOUT);
            Assert.True(workspace.IsCalculatingAllRecommendations);
            Assert.Equal("1 / 1+", workspace.RecommendationSummary);

            workspace.CancelAllRecommendationsCommand.Execute(null);
            await recommendationProvider.ExhaustiveCallCanceled.WaitAsync(TEST_TIMEOUT);
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            Assert.Equal("1 / 1+", workspace.RecommendationSummary);
            Assert.True(workspace.HasAdditionalRecommendations);
            Assert.True(workspace.CanCalculateAllRecommendations);
            Assert.False(workspace.IsCalculatingAllRecommendations);
        }
    }

    [AvaloniaFact]
    public async Task ExhaustiveCalculationFailureKeepsTheInitialResultAndOffersRetryAsync()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        ControlledExhaustiveScheduleRecommendationProvider recommendationProvider = new ControlledExhaustiveScheduleRecommendationProvider(document.Catalog, EControlledExhaustiveOutcome.ThrowException);
        RecommendationCalculationPolicy policy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(1), TimeSpan.Zero);
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(document, recommendationProvider, policy))
        {
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            workspace.CalculateAllRecommendationsCommand.Execute(null);
            await workspace.RecommendationRefreshTask.WaitAsync(TEST_TIMEOUT);

            Assert.Equal("1 / 1+", workspace.RecommendationSummary);
            Assert.True(workspace.HasAdditionalRecommendations);
            Assert.True(workspace.CanCalculateAllRecommendations);
            Assert.Equal("전체 시간표 계산을 완료하지 못했습니다", workspace.AdditionalRecommendationTitle);
            Assert.Equal("먼저 준비한 1개는 계속 확인할 수 있습니다.", workspace.AdditionalRecommendationMessage);
            Assert.Equal("다시 계산", workspace.CalculateAllRecommendationsActionText);
        }
    }

    private static CourseSearchItem findCourse(
        PlannerWorkspaceViewModel workspace,
        string courseCode)
    {
        foreach (CourseSearchItem course in workspace.VisibleCourses)
        {
            if (string.Equals(
                course.Code,
                courseCode,
                StringComparison.Ordinal))
            {
                return course;
            }
        }

        throw new InvalidOperationException(
            "The requested test course was not visible.");
    }

    private static IReadOnlyList<string> getActiveOfferingIds(
        PlannerWorkspaceViewModel workspace)
    {
        List<string> offeringIds = new List<string>();
        foreach (ScheduleEntry entry in workspace.ActiveRecommendation.Entries)
        {
            CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
            if (courseEntryOrNull != null
                && offeringIds.Contains(courseEntryOrNull.OfferingId.Value) == false)
            {
                offeringIds.Add(courseEntryOrNull.OfferingId.Value);
            }
        }

        offeringIds.Sort(StringComparer.Ordinal);
        return offeringIds.AsReadOnly();
    }

    private sealed class PausedUnlimitedScheduleRecommendationProvider :
        IScheduleRecommendationProvider,
        IDisposable
    {
        private readonly CatalogScheduleRecommendationProvider mInnerProvider;

        private readonly TaskCompletionSource<bool> mExhaustiveCallStartedSource;

        private readonly ManualResetEventSlim mCompleteExhaustiveCalculationEvent;

        public Task ExhaustiveCallStarted
        {
            get
            {
                return mExhaustiveCallStartedSource.Task;
            }
        }

        public PausedUnlimitedScheduleRecommendationProvider(
            CourseCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            mInnerProvider = new CatalogScheduleRecommendationProvider(catalog);
            mExhaustiveCallStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            mCompleteExhaustiveCalculationEvent = new ManualResetEventSlim(false);
        }

        public ScheduleRecommendationResult Generate(
            PlanningPlan plan,
            ScheduleRecommendationLimit recommendationLimit,
            CancellationToken cancellationToken)
        {
            if (recommendationLimit.IsUnlimited == false)
            {
                return mInnerProvider.Generate(
                    plan,
                    recommendationLimit,
                    cancellationToken);
            }

            mExhaustiveCallStartedSource.TrySetResult(true);
            mCompleteExhaustiveCalculationEvent.Wait(cancellationToken);
            return mInnerProvider.Generate(
                plan,
                recommendationLimit,
                cancellationToken);
        }

        public void CompleteExhaustiveCalculation()
        {
            mCompleteExhaustiveCalculationEvent.Set();
        }

        public void Dispose()
        {
            mCompleteExhaustiveCalculationEvent.Dispose();
        }
    }
}
