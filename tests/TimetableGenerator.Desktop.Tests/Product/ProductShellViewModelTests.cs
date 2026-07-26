using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Product;

public sealed class ProductShellViewModelTests
{
    [AvaloniaFact]
    public async Task StartTransitionsFromLoadingToReadyAsync()
    {
        PlannerWorkspaceViewModel expectedWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(expectedWorkspace);
                return Task.FromResult(presentation);
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            Assert.True(shell.IsLoading);
            Assert.Equal("Timetable Generator", shell.AccessibleWindowName);

            await shell.StartAsync();

            Assert.True(shell.IsReady);
            Assert.False(shell.IsLoading);
            Assert.False(shell.HasError);
            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
            Assert.Equal("Timetable Generator - 한동대학교", shell.AccessibleWindowName);
        }
    }

    [AvaloniaFact]
    public async Task StagedCatalogUpdateShowsAndDismissesNoticeAsync()
    {
        PlannerWorkspaceViewModel expectedWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(expectedWorkspace);
                return Task.FromResult(presentation);
            });
        QueueProductCatalogUpdateService catalogUpdateService =
            createCatalogUpdateService(
                delegate
                {
                    ProductCatalogUpdateResult updateResult = new ProductCatalogUpdateResult(EProductCatalogUpdateStatus.Staged, new CatalogRevision(2));
                    return Task.FromResult(updateResult);
                });
        using (ProductShellViewModel shell = new ProductShellViewModel(
            loader,
            catalogUpdateService))
        {
            await shell.StartAsync();
            await shell.CatalogUpdateTask;

            Assert.True(shell.IsReady);
            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
            Assert.Equal(1, catalogUpdateService.CheckCount);
            Assert.True(shell.HasCatalogUpdateNotice);
            Assert.True(shell.HasProductNotice);
            Assert.Equal("과목 데이터 r0002 준비됨: 다음 실행에서 확인 후 적용", shell.CatalogUpdateNotice);

            shell.DismissProductNoticeCommand.Execute(null);

            Assert.False(shell.HasCatalogUpdateNotice);
            Assert.False(shell.HasProductNotice);
            Assert.Empty(shell.CatalogUpdateNotice);
        }
    }

    [AvaloniaFact]
    public async Task RecoveryNoticePrecedesAndThenRevealsCatalogUpdateAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                EProductWorkspaceRecoveryFlags recoveryFlags = EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration;
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentationWithRecoveryFlags(workspace, recoveryFlags);
                return Task.FromResult(presentation);
            });
        QueueProductCatalogUpdateService catalogUpdateService =
            createCatalogUpdateService(
                delegate
                {
                    ProductCatalogUpdateResult updateResult = new ProductCatalogUpdateResult(EProductCatalogUpdateStatus.Staged, new CatalogRevision(2));
                    return Task.FromResult(updateResult);
                });
        using (ProductShellViewModel shell = new ProductShellViewModel(
            loader,
            catalogUpdateService))
        {
            await shell.StartAsync();
            await shell.CatalogUpdateTask;

            Assert.True(shell.HasStartupRecoveryNotice);
            Assert.True(shell.HasCatalogUpdateNotice);
            Assert.True(shell.HasProductNotice);
            Assert.Contains("이전 안전 저장본", shell.ProductNotice);
            Assert.Contains("최근 변경 일부", shell.ProductNotice);
            Assert.DoesNotContain("r0002", shell.ProductNotice);

            shell.DismissProductNoticeCommand.Execute(null);

            Assert.False(shell.HasStartupRecoveryNotice);
            Assert.True(shell.HasCatalogUpdateNotice);
            Assert.True(shell.HasProductNotice);
            Assert.Contains("r0002", shell.ProductNotice);

            shell.DismissProductNoticeCommand.Execute(null);

            Assert.False(shell.HasCatalogUpdateNotice);
            Assert.False(shell.HasProductNotice);
            Assert.Empty(shell.ProductNotice);
        }
    }

    [AvaloniaFact]
    public async Task NormalWorkspaceSetupDoesNotShowRecoveryNoticeAsync()
    {
        await assertRecoveryFlagsDoNotShowNoticeAsync(EProductWorkspaceRecoveryFlags.WorkspaceCreated);
        await assertRecoveryFlagsDoNotShowNoticeAsync(
            EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound);
    }

    [AvaloniaFact]
    public async Task CatalogUpdateFailureKeepsLoadedWorkspaceReadyAsync()
    {
        PlannerWorkspaceViewModel expectedWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(expectedWorkspace);
                return Task.FromResult(presentation);
            });
        QueueProductCatalogUpdateService catalogUpdateService =
            createCatalogUpdateService(
                delegate
                {
                    return Task.FromException<ProductCatalogUpdateResult>(
                        new InvalidOperationException(
                            "Expected background update failure."));
                });
        using (ProductShellViewModel shell = new ProductShellViewModel(
            loader,
            catalogUpdateService))
        {
            await shell.StartAsync();
            await shell.CatalogUpdateTask;

            Assert.True(shell.IsReady);
            Assert.False(shell.HasError);
            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
            Assert.False(shell.HasCatalogUpdateNotice);
        }
    }

    [AvaloniaFact]
    public async Task RetryIgnoresCatalogUpdateFromPreviousWorkspaceAsync()
    {
        PlannerWorkspaceViewModel staleWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        PlannerWorkspaceViewModel expectedWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspacePresentation stalePresentation = PlannerWorkspaceTestFactory.CreatePresentation(staleWorkspace);
        ProductWorkspacePresentation expectedPresentation = PlannerWorkspaceTestFactory.CreatePresentation(expectedWorkspace);
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<ProductWorkspacePresentation>>[]
            {
                delegate
                {
                    return Task.FromResult(stalePresentation);
                },
                delegate
                {
                    return Task.FromResult(expectedPresentation);
                },
            });
        TaskCompletionSource<ProductCatalogUpdateResult> staleUpdateCompletion = new TaskCompletionSource<ProductCatalogUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        QueueProductCatalogUpdateService catalogUpdateService =
            new QueueProductCatalogUpdateService(
                new Func<
                    VerifiedCatalogPackage,
                    PlanningWorkspace,
                    CancellationToken,
                    Task<ProductCatalogUpdateResult>>[]
                {
                    delegate
                    {
                        return staleUpdateCompletion.Task;
                    },
                    delegate
                    {
                        ProductCatalogUpdateResult updateResult = new ProductCatalogUpdateResult(EProductCatalogUpdateStatus.Current, new CatalogRevision(1));
                        return Task.FromResult(updateResult);
                    },
                });
        using (ProductShellViewModel shell = new ProductShellViewModel(
            loader,
            catalogUpdateService))
        {
            await shell.StartAsync();
            Task staleUpdateTask = shell.CatalogUpdateTask;

            await shell.StartAsync();
            await shell.CatalogUpdateTask;
            staleUpdateCompletion.SetResult(
                new ProductCatalogUpdateResult(
                    EProductCatalogUpdateStatus.Staged,
                    new CatalogRevision(2)));
            await staleUpdateTask;

            Assert.Equal(2, catalogUpdateService.CheckCount);
            Assert.True(shell.IsReady);
            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
            Assert.False(shell.HasCatalogUpdateNotice);
        }
    }

    [AvaloniaFact]
    public async Task DisposedShellIgnoresLateCatalogUpdateAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(workspace);
                return Task.FromResult(presentation);
            });
        TaskCompletionSource<ProductCatalogUpdateResult> updateCompletion = new TaskCompletionSource<ProductCatalogUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        QueueProductCatalogUpdateService catalogUpdateService =
            createCatalogUpdateService(
                delegate
                {
                    return updateCompletion.Task;
                });
        ProductShellViewModel shell = new ProductShellViewModel(loader, catalogUpdateService);
        await shell.StartAsync();
        Task updateTask = shell.CatalogUpdateTask;

        shell.Dispose();
        updateCompletion.SetResult(
            new ProductCatalogUpdateResult(
                EProductCatalogUpdateStatus.Staged,
                new CatalogRevision(2)));
        await updateTask;

        Assert.False(shell.HasCatalogUpdateNotice);
        Assert.Empty(shell.CatalogUpdateNotice);
    }

    [AvaloniaFact]
    public async Task RemoteStartupDoesNotImmediatelyDownloadCatalogAgainAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(workspace, EProductCatalogOrigin.RemoteDownload);
                return Task.FromResult(presentation);
            });
        QueueProductCatalogUpdateService catalogUpdateService = new QueueProductCatalogUpdateService(Array.Empty<Func<VerifiedCatalogPackage, PlanningWorkspace, CancellationToken, Task<ProductCatalogUpdateResult>>>());
        using (ProductShellViewModel shell = new ProductShellViewModel(
            loader,
            catalogUpdateService))
        {
            await shell.StartAsync();
            await shell.CatalogUpdateTask;

            Assert.True(shell.IsReady);
            Assert.Equal(0, catalogUpdateService.CheckCount);
        }
    }

    [AvaloniaFact]
    public async Task MissingConfigurationProducesActionableProductErrorAsync()
    {
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromException<ProductWorkspacePresentation>(
                    new CatalogSourceConfigurationException("Missing test source."));
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            await shell.StartAsync();

            Assert.True(shell.HasError);
            Assert.Null(shell.WorkspaceOrNull);
            Assert.Equal("과목 데이터를 불러오지 못했습니다", shell.StatusTitle);
            Assert.Contains("배포 설정", shell.StatusMessage);
        }
    }

    [AvaloniaFact]
    public async Task ConcurrentWorkspaceChangeProducesARecoveryMessageAsync()
    {
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                PlanningWorkspaceConcurrencyException failure = new PlanningWorkspaceConcurrencyException(new PlanningWorkspaceConcurrencyToken(1L), new PlanningWorkspaceConcurrencyToken(2L));
                return Task.FromException<ProductWorkspacePresentation>(failure);
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            await shell.StartAsync();

            Assert.True(shell.HasError);
            Assert.Contains("다른 앱 창", shell.StatusMessage);
            Assert.Contains("다시 열어", shell.StatusMessage);
        }
    }

    [AvaloniaFact]
    public async Task RetryCommandLoadsAWorkspaceAfterFailureAsync()
    {
        PlannerWorkspaceViewModel expectedWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<ProductWorkspacePresentation>>[]
            {
                delegate
                {
                    return Task.FromException<ProductWorkspacePresentation>(
                        new InvalidOperationException("First load failed."));
                },
                delegate
                {
ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(expectedWorkspace);
                    return Task.FromResult(presentation);
                },
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            await shell.StartAsync();
            Assert.True(shell.HasError);
            AsyncDelegateCommand retryCommand = Assert.IsType<AsyncDelegateCommand>(shell.RetryCommand);

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
        TaskCompletionSource<bool> cancellationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        ProductShellViewModel shell = createShell(loader);
        Task loadTask = shell.StartAsync();

        shell.Dispose();

        Assert.True(await cancellationObserved.Task);
        await loadTask;
    }

    [AvaloniaFact]
    public async Task StaleLoadResultIsDisposedAfterANewerLoadCompletesAsync()
    {
        PlannerWorkspaceViewModel staleWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        PlannerWorkspaceViewModel expectedWorkspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        TaskCompletionSource<ProductWorkspacePresentation> firstLoadCompletion = new TaskCompletionSource<ProductWorkspacePresentation>(TaskCreationOptions.RunContinuationsAsynchronously);
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<ProductWorkspacePresentation>>[]
            {
                delegate
                {
                    return firstLoadCompletion.Task;
                },
                delegate
                {
ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(expectedWorkspace);
                    return Task.FromResult(presentation);
                },
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            Task staleLoadTask = shell.StartAsync();

            await shell.StartAsync();
            ProductWorkspacePresentation stalePresentation = PlannerWorkspaceTestFactory.CreatePresentation(staleWorkspace);
            firstLoadCompletion.SetResult(stalePresentation);
            await staleLoadTask;

            Assert.Same(expectedWorkspace, shell.WorkspaceOrNull);
            await Assert.ThrowsAsync<ObjectDisposedException>(
                delegate
                {
                    return staleWorkspace.FlushAutosaveAsync(CancellationToken.None);
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
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentation(workspace);
                return Task.FromResult(presentation);
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            await shell.StartAsync();
            Assert.Equal("Timetable Generator - 한동대학교", shell.AccessibleWindowName);

            shell.beginShutdown();

            Assert.True(shell.IsShutdownInProgress);
            Assert.True(shell.IsShutdownOverlayVisible);
            Assert.False(shell.IsProductInteractionEnabled);

            shell.showShutdownFailure(new InvalidOperationException("Expected save failure."));

            Assert.True(shell.HasShutdownError);
            Assert.False(shell.IsProductInteractionEnabled);
            Assert.Contains("닫지 않았", shell.ShutdownTitle);

            shell.DismissShutdownErrorCommand.Execute(null);

            Assert.False(shell.IsShutdownOverlayVisible);
            Assert.True(shell.IsProductInteractionEnabled);
        }
    }

    [AvaloniaFact]
    public async Task InvalidRemoteDataProducesAnIntegrityMessageAsync()
    {
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                return Task.FromException<ProductWorkspacePresentation>(
                    new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.InvalidRemoteData,
                        "Expected invalid remote data."));
            });
        using (ProductShellViewModel shell = createShell(loader))
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
                return Task.FromException<ProductWorkspacePresentation>(
                    new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.LocalPersistence,
                        "Expected local persistence failure."));
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            await shell.StartAsync();

            Assert.True(shell.HasError);
            Assert.Contains("저장", shell.StatusMessage);
            Assert.Contains("권한", shell.StatusMessage);
        }
    }

    private static QueueProductWorkspaceLoader createLoader(
        Func<CancellationToken, Task<ProductWorkspacePresentation>> load)
    {
        return new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<ProductWorkspacePresentation>>[]
            {
                load,
            });
    }

    private static QueueProductCatalogUpdateService createCatalogUpdateService(
        Func<
            VerifiedCatalogPackage,
            PlanningWorkspace,
            CancellationToken,
            Task<ProductCatalogUpdateResult>> check)
    {
        return new QueueProductCatalogUpdateService(
            new Func<
                VerifiedCatalogPackage,
                PlanningWorkspace,
                CancellationToken,
                Task<ProductCatalogUpdateResult>>[]
            {
                check,
            });
    }

    private static ProductShellViewModel createShell(QueueProductWorkspaceLoader loader)
    {
        QueueProductCatalogUpdateService catalogUpdateService =
            createCatalogUpdateService(
                delegate
                {
                    ProductCatalogUpdateResult updateResult = new ProductCatalogUpdateResult(EProductCatalogUpdateStatus.Current, new CatalogRevision(1));
                    return Task.FromResult(updateResult);
                });
        return new ProductShellViewModel(loader, catalogUpdateService);
    }

    private static async Task assertRecoveryFlagsDoNotShowNoticeAsync(
        EProductWorkspaceRecoveryFlags recoveryFlags)
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        QueueProductWorkspaceLoader loader = createLoader(
            delegate
            {
                ProductWorkspacePresentation presentation = PlannerWorkspaceTestFactory.CreatePresentationWithRecoveryFlags(workspace, recoveryFlags);
                return Task.FromResult(presentation);
            });
        using (ProductShellViewModel shell = createShell(loader))
        {
            await shell.StartAsync();
            await shell.CatalogUpdateTask;

            Assert.False(shell.HasStartupRecoveryNotice);
            Assert.Empty(shell.StartupRecoveryNotice);
            Assert.False(shell.HasProductNotice);
        }
    }
}
