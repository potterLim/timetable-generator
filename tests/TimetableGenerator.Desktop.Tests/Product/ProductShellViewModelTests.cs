using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Infrastructure.Catalogs;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Product;

public sealed class ProductShellViewModelTests
{
    [AvaloniaFact]
    public async Task StartTransitionsFromLoadingToReadyAsync()
    {
        PlannerWorkspaceViewModel expectedWorkspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromResult(expectedWorkspace);
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            Assert.True(shell.IsLoading);

            await shell.StartAsync();

            Assert.True(shell.IsReady);
            Assert.False(shell.IsLoading);
            Assert.False(shell.HasError);
            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
        }
    }

    [AvaloniaFact]
    public async Task MissingConfigurationProducesActionableProductErrorAsync()
    {
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromException<PlannerWorkspaceViewModel>(
                    new CatalogSourceConfigurationException("Missing test source."));
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            await shell.StartAsync();

            Assert.True(shell.HasError);
            Assert.Null(shell.WorkspaceOrNull);
            Assert.Equal("과목 데이터를 불러오지 못했어요", shell.StatusTitle);
            Assert.Contains("배포 설정", shell.StatusMessage);
        }
    }

    [AvaloniaFact]
    public async Task RetryCommandLoadsAWorkspaceAfterFailureAsync()
    {
        PlannerWorkspaceViewModel expectedWorkspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<PlannerWorkspaceViewModel>>[]
            {
                delegate
                {
                    return Task.FromException<PlannerWorkspaceViewModel>(
                        new InvalidOperationException("First load failed."));
                },
                delegate
                {
                    return Task.FromResult(expectedWorkspace);
                },
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            await shell.StartAsync();
            Assert.True(shell.HasError);
            AsyncDelegateCommand retryCommand = Assert.IsType<AsyncDelegateCommand>(
                shell.RetryCommand);

            retryCommand.Execute(null);
            await retryCommand.ExecutionTask;

            Assert.Equal(2, loader.LoadCount);
            Assert.True(shell.IsReady);
            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
        }
    }

    [AvaloniaFact]
    public async Task DisposingShellCancelsAnActiveLoadAsync()
    {
        TaskCompletionSource<bool> cancellationObserved =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        QueueProductWorkspaceLoader loader = createLoader(
            async delegate (CancellationToken cancellationToken)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.SetResult(true);
                    throw;
                }

                throw new InvalidOperationException("The infinite delay completed.");
            });
        ProductShellViewModel shell = new ProductShellViewModel(loader);
        Task loadTask = shell.StartAsync();

        shell.Dispose();

        Assert.True(await cancellationObserved.Task);
        await loadTask;
    }

    [AvaloniaFact]
    public async Task StaleLoadResultIsDisposedAfterANewerLoadCompletesAsync()
    {
        PlannerWorkspaceViewModel staleWorkspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        PlannerWorkspaceViewModel expectedWorkspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        TaskCompletionSource<PlannerWorkspaceViewModel> firstLoadCompletion =
            new TaskCompletionSource<PlannerWorkspaceViewModel>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<PlannerWorkspaceViewModel>>[]
            {
                delegate
                {
                    return firstLoadCompletion.Task;
                },
                delegate
                {
                    return Task.FromResult(expectedWorkspace);
                },
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            Task staleLoadTask = shell.StartAsync();

            await shell.StartAsync();
            firstLoadCompletion.SetResult(staleWorkspace);
            await staleLoadTask;

            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
            await Assert.ThrowsAsync<ObjectDisposedException>(
                delegate
                {
                    return staleWorkspace.FlushAutosaveAsync(
                        CancellationToken.None);
                });
        }
    }

    [AvaloniaFact]
    public async Task ShutdownStateBlocksInteractionAndReportsSaveFailureAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromResult(workspace);
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            await shell.StartAsync();
            Assert.Contains("한동대학교", shell.AccessibleWindowName);

            shell.beginShutdown();

            Assert.True(shell.IsShutdownInProgress);
            Assert.True(shell.IsShutdownOverlayVisible);
            Assert.False(shell.IsProductInteractionEnabled);

            shell.showShutdownFailure(
                new InvalidOperationException("Expected save failure."));

            Assert.True(shell.HasShutdownError);
            Assert.True(shell.IsProductInteractionEnabled);
            Assert.Contains("닫지 않았", shell.ShutdownTitle);

            shell.DismissShutdownErrorCommand.Execute(null);

            Assert.False(shell.IsShutdownOverlayVisible);
        }
    }

    [AvaloniaFact]
    public async Task InvalidRemoteDataProducesAnIntegrityMessageAsync()
    {
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromException<PlannerWorkspaceViewModel>(
                    new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.InvalidRemoteData,
                        "Expected invalid remote data."));
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            await shell.StartAsync();

            Assert.True(shell.HasError);
            Assert.Contains("안전하게 검증", shell.StatusMessage);
            Assert.DoesNotContain("인터넷 연결", shell.StatusMessage);
        }
    }

    [AvaloniaFact]
    public async Task RemoteCacheFailureProducesALocalStorageMessageAsync()
    {
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromException<PlannerWorkspaceViewModel>(
                    new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.LocalPersistence,
                        "Expected local persistence failure."));
            });
        using (ProductShellViewModel shell = new ProductShellViewModel(loader))
        {
            await shell.StartAsync();

            Assert.True(shell.HasError);
            Assert.Contains("저장", shell.StatusMessage);
            Assert.Contains("권한", shell.StatusMessage);
        }
    }

    private static QueueProductWorkspaceLoader createLoader(
        Func<CancellationToken, Task<PlannerWorkspaceViewModel>> load)
    {
        return new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<PlannerWorkspaceViewModel>>[] { load });
    }
}
