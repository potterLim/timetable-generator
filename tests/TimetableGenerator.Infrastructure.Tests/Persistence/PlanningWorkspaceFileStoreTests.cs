using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Persistence;

namespace TimetableGenerator.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class PlanningWorkspaceFileStoreTests
{
    [TestMethod]
    public async Task StoreSavesAndLoadsTheLatestGenerationAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            PlanningWorkspace workspace = createWorkspace("기본 시간표");

            await store.SaveAsync(workspace, CancellationToken.None);
            PlanningWorkspaceLoadResult result = await store.LoadAsync(
                CancellationToken.None);

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.LoadedLatestGeneration,
                result.Status);
            Assert.AreEqual(
                "기본 시간표",
                getWorkspace(result).GetActivePlan().Name.Value);
            Assert.HasCount(1, Directory.GetFiles(testDirectoryPath, "*.json"));
            Assert.IsEmpty(Directory.GetFiles(testDirectoryPath, "*.tmp"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task StoreRecoversThePreviousImmutableGenerationAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(createWorkspace("이전 시간표"), CancellationToken.None);
            await store.SaveAsync(createWorkspace("현재 시간표"), CancellationToken.None);
            await File.WriteAllTextAsync(
                getGenerationPath(testDirectoryPath, 2),
                "{ damaged",
                CancellationToken.None);

            PlanningWorkspaceLoadResult result = await store.LoadAsync(
                CancellationToken.None);

            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
                result.Status);
            Assert.AreEqual(
                "이전 시간표",
                getWorkspace(result).GetActivePlan().Name.Value);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SaveAfterRecoveryCannotPoisonOlderValidGenerationsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(
                createWorkspace("복구 기준 시간표"),
                CancellationToken.None);
            await store.SaveAsync(
                createWorkspace("손상될 시간표"),
                CancellationToken.None);
            await File.WriteAllTextAsync(
                getGenerationPath(testDirectoryPath, 2),
                "{ damaged primary",
                CancellationToken.None);
            PlanningWorkspaceLoadResult firstRecovery = await store.LoadAsync(
                CancellationToken.None);
            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
                firstRecovery.Status);

            await store.SaveAsync(
                createWorkspace("복구 후 시간표"),
                CancellationToken.None);
            await File.WriteAllTextAsync(
                getGenerationPath(testDirectoryPath, 3),
                "{ damaged again",
                CancellationToken.None);
            PlanningWorkspaceLoadResult secondRecovery = await store.LoadAsync(
                CancellationToken.None);

            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
                secondRecovery.Status);
            Assert.AreEqual(
                "복구 기준 시간표",
                getWorkspace(secondRecovery).GetActivePlan().Name.Value);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task StoreReportsNotFoundWithoutCreatingProductDataAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);

            PlanningWorkspaceLoadResult result = await store.LoadAsync(
                CancellationToken.None);

            Assert.IsFalse(result.IsFound);
            Assert.AreEqual(EPlanningWorkspaceLoadStatus.NotFound, result.Status);
            Assert.IsNull(result.WorkspaceOrNull);
            Assert.IsFalse(Directory.Exists(testDirectoryPath));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task CancelledSavePreservesTheLastCompleteGenerationAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            using (CancellationTokenSource cancellationSource =
                new CancellationTokenSource())
            {
                await store.SaveAsync(
                    createWorkspace("안전한 시간표"),
                    CancellationToken.None);
                cancellationSource.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => store.SaveAsync(
                        createWorkspace("저장되면 안 되는 시간표"),
                        cancellationSource.Token));
            }

            PlanningWorkspaceLoadResult result = await store.LoadAsync(
                CancellationToken.None);
            Assert.AreEqual(
                "안전한 시간표",
                getWorkspace(result).GetActivePlan().Name.Value);
            Assert.IsEmpty(Directory.GetFiles(testDirectoryPath, "*.tmp"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task StoreFailsClearlyWhenAllGenerationsAreDamagedAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            Directory.CreateDirectory(testDirectoryPath);
            await File.WriteAllTextAsync(
                getGenerationPath(testDirectoryPath, 1),
                "{ damaged first",
                CancellationToken.None);
            await File.WriteAllTextAsync(
                getGenerationPath(testDirectoryPath, 2),
                "{ damaged second",
                CancellationToken.None);
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);

            await Assert.ThrowsExactlyAsync<WorkspacePersistenceException>(
                () => store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task NewerSchemaBlocksFallbackAndPreventsDowngradeOverwriteAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(createWorkspace("이전 시간표"), CancellationToken.None);
            await store.SaveAsync(createWorkspace("미래 시간표"), CancellationToken.None);
            string latestPath = getGenerationPath(testDirectoryPath, 2);
            string latestContent = await File.ReadAllTextAsync(
                latestPath,
                CancellationToken.None);
            string futureContent = latestContent.Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 2,",
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(
                latestPath,
                futureContent,
                new UTF8Encoding(false),
                CancellationToken.None);

            PlanningWorkspaceUpgradeRequiredException exception =
                await Assert.ThrowsExactlyAsync<PlanningWorkspaceUpgradeRequiredException>(
                    () => store.LoadAsync(CancellationToken.None));
            byte[][] contentBeforeSave = await readGenerationContentsAsync(
                testDirectoryPath);

            PlanningWorkspaceUpgradeRequiredException saveException =
                await Assert.ThrowsExactlyAsync<PlanningWorkspaceUpgradeRequiredException>(
                    () => store.SaveAsync(
                        createWorkspace("덮어쓰면 안 되는 시간표"),
                        CancellationToken.None));
            byte[][] contentAfterSave = await readGenerationContentsAsync(
                testDirectoryPath);

            Assert.AreEqual(2, exception.UnsupportedSchemaVersion);
            Assert.AreEqual(2, saveException.UnsupportedSchemaVersion);
            Assert.HasCount(contentBeforeSave.Length, contentAfterSave);
            for (int index = 0; index < contentBeforeSave.Length; index++)
            {
                CollectionAssert.AreEqual(contentBeforeSave[index], contentAfterSave[index]);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task CorruptNewestGenerationCannotHideAnOlderFutureSchemaFromSaveAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(createWorkspace("이전 시간표"), CancellationToken.None);
            await store.SaveAsync(createWorkspace("미래 시간표"), CancellationToken.None);
            string futurePath = getGenerationPath(testDirectoryPath, 2);
            string futureContent = await File.ReadAllTextAsync(
                futurePath,
                CancellationToken.None);
            futureContent = futureContent.Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 2,",
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(
                futurePath,
                futureContent,
                new UTF8Encoding(false),
                CancellationToken.None);
            await File.WriteAllTextAsync(
                getGenerationPath(testDirectoryPath, 3),
                "{ corrupt newest generation",
                CancellationToken.None);
            byte[][] contentBeforeSave = await readGenerationContentsAsync(
                testDirectoryPath);

            await Assert.ThrowsExactlyAsync<PlanningWorkspaceUpgradeRequiredException>(
                () => store.LoadAsync(CancellationToken.None));
            await Assert.ThrowsExactlyAsync<PlanningWorkspaceUpgradeRequiredException>(
                () => store.SaveAsync(
                    createWorkspace("덮어쓰면 안 되는 시간표"),
                    CancellationToken.None));
            byte[][] contentAfterSave = await readGenerationContentsAsync(
                testDirectoryPath);

            Assert.HasCount(contentBeforeSave.Length, contentAfterSave);
            for (int index = 0; index < contentBeforeSave.Length; index++)
            {
                CollectionAssert.AreEqual(contentBeforeSave[index], contentAfterSave[index]);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task InvalidTermFallsBackThroughTheTypedDocumentErrorPathAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(createWorkspace("정상 시간표"), CancellationToken.None);
            await store.SaveAsync(createWorkspace("손상 시간표"), CancellationToken.None);
            string latestPath = getGenerationPath(testDirectoryPath, 2);
            string latestContent = await File.ReadAllTextAsync(
                latestPath,
                CancellationToken.None);
            string invalidTermContent = latestContent.Replace(
                "\"term\": \"2026-2\"",
                "\"term\": \"invalid-term\"",
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(
                latestPath,
                invalidTermContent,
                new UTF8Encoding(false),
                CancellationToken.None);

            PlanningWorkspaceLoadResult result = await store.LoadAsync(
                CancellationToken.None);

            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
                result.Status);
            Assert.AreEqual(
                "정상 시간표",
                getWorkspace(result).GetActivePlan().Name.Value);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task OversizedGenerationFallsBackBeforeAllocatingItsContentAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(
                testDirectoryPath,
                new WorkspaceDocumentSizeLimit(2_048));
            await store.SaveAsync(createWorkspace("정상 시간표"), CancellationToken.None);
            await File.WriteAllBytesAsync(
                getGenerationPath(testDirectoryPath, 2),
                new byte[2_049],
                CancellationToken.None);

            PlanningWorkspaceLoadResult result = await store.LoadAsync(
                CancellationToken.None);

            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
                result.Status);
            Assert.AreEqual(
                "정상 시간표",
                getWorkspace(result).GetActivePlan().Name.Value);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ConcurrentStoreInstancesCommitDistinctValidGenerationsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore firstStore = createStore(testDirectoryPath);
            PlanningWorkspaceFileStore secondStore = createStore(testDirectoryPath);

            await Task.WhenAll(
                firstStore.SaveAsync(
                    createWorkspace("첫 번째 시간표"),
                    CancellationToken.None),
                secondStore.SaveAsync(
                    createWorkspace("두 번째 시간표"),
                    CancellationToken.None));
            PlanningWorkspaceLoadResult result = await firstStore.LoadAsync(
                CancellationToken.None);

            Assert.AreEqual(
                EPlanningWorkspaceLoadStatus.LoadedLatestGeneration,
                result.Status);
            Assert.HasCount(2, Directory.GetFiles(testDirectoryPath, "*.json"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task StoreRetainsOnlyTheLatestRecoveryWindowAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            for (int index = 1; index <= 7; index++)
            {
                await store.SaveAsync(
                    createWorkspace(
                        "시간표 " + index.ToString(CultureInfo.InvariantCulture)),
                    CancellationToken.None);
            }

            Assert.HasCount(5, Directory.GetFiles(testDirectoryPath, "*.json"));
            Assert.IsFalse(File.Exists(getGenerationPath(testDirectoryPath, 1)));
            Assert.IsTrue(File.Exists(getGenerationPath(testDirectoryPath, 7)));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExhaustedGenerationRangeKeepsTheWorkspaceSpecificFailureAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            PlanningWorkspaceFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(
                createWorkspace("기존 시간표"),
                CancellationToken.None);
            File.Move(
                getGenerationPath(testDirectoryPath, 1L),
                getGenerationPath(testDirectoryPath, long.MaxValue));

            InvalidOperationException exception =
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => store.SaveAsync(
                        createWorkspace("새 시간표"),
                        CancellationToken.None));

            Assert.AreEqual(
                "The planning workspace generation range is exhausted.",
                exception.Message);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    private static PlanningWorkspaceFileStore createStore(string testDirectoryPath)
    {
        return createStore(
            testDirectoryPath,
            WorkspaceDocumentSizeLimit.ProductDefault);
    }

    private static PlanningWorkspaceFileStore createStore(
        string testDirectoryPath,
        WorkspaceDocumentSizeLimit sizeLimit)
    {
        string workspacePath = Path.Combine(testDirectoryPath, "workspace-v1.json");
        return new PlanningWorkspaceFileStore(
            new WorkspaceFilePath(workspacePath),
            new PlanningWorkspaceJsonCodec(),
            sizeLimit);
    }

    private static PlanningWorkspace createWorkspace(string name)
    {
        PlanId planId = new PlanId(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        PlanCatalogBinding binding = new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningPlan plan = new PlanningPlan(
            planId,
            new PlanName(name),
            binding,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
        return new PlanningWorkspace(planId, new PlanningPlan[] { plan });
    }

    private static string getGenerationPath(
        string testDirectoryPath,
        long generationValue)
    {
        WorkspaceGeneration generation = new WorkspaceGeneration(generationValue);
        return Path.Combine(
            testDirectoryPath,
            "workspace-v1." + generation.FileComponent + ".json");
    }

    private static string createTestDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Infrastructure.Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static PlanningWorkspace getWorkspace(PlanningWorkspaceLoadResult result)
    {
        PlanningWorkspace? workspaceOrNull = result.WorkspaceOrNull;
        Assert.IsNotNull(workspaceOrNull);
        return workspaceOrNull;
    }

    private static async Task<byte[][]> readGenerationContentsAsync(
        string testDirectoryPath)
    {
        string[] paths = Directory.GetFiles(testDirectoryPath, "*.json");
        Array.Sort(paths, StringComparer.Ordinal);
        byte[][] contents = new byte[paths.Length][];
        for (int index = 0; index < paths.Length; index++)
        {
            contents[index] = await File.ReadAllBytesAsync(
                paths[index],
                CancellationToken.None);
        }

        return contents;
    }

    private static void deleteTestDirectory(string testDirectoryPath)
    {
        if (Directory.Exists(testDirectoryPath))
        {
            Directory.Delete(testDirectoryPath, true);
        }
    }
}
