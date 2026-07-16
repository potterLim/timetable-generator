using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

[TestClass]
public sealed class CatalogCacheFileStoreTests
{
    private const int CACHE_SCHEMA_VERSION_OFFSET = 8;

    [TestMethod]
    public async Task LoadReturnsNotFoundWhenNoGenerationExistsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);

            CatalogCacheLoadResult result = await store.LoadAsync(CancellationToken.None);

            Assert.AreEqual(ECatalogCacheLoadStatus.NotFound, result.Status);
            Assert.IsFalse(result.IsFound);
            Assert.IsNull(result.PackageOrNull);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SaveCreatesImmutableGenerationsAndLoadsNewestAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            VerifiedCatalogPackage firstPackage =
                CatalogSynchronizationTestDocuments.CreateVerifiedPackage();
            VerifiedCatalogPackage secondPackage =
                CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName(
                    "최신 자료구조");
            await store.SaveAsync(firstPackage, CancellationToken.None);
            string firstGenerationPath = getGenerationPath(testDirectoryPath, 1L);
            byte[] firstGenerationBefore = await File.ReadAllBytesAsync(
                firstGenerationPath,
                CancellationToken.None);

            await store.SaveAsync(secondPackage, CancellationToken.None);
            byte[] firstGenerationAfter = await File.ReadAllBytesAsync(
                firstGenerationPath,
                CancellationToken.None);
            CatalogCacheLoadResult result = await store.LoadAsync(CancellationToken.None);

            CollectionAssert.AreEqual(firstGenerationBefore, firstGenerationAfter);
            Assert.AreEqual(
                ECatalogCacheLoadStatus.LoadedLatestGeneration,
                result.Status);
            Assert.AreEqual(
                "최신 자료구조",
                result.GetPackage().Document.Catalog.Courses[0].KoreanName.Value);
            Assert.HasCount(2, Directory.GetFiles(testDirectoryPath, "catalog.g*.cache"));
            Assert.IsEmpty(Directory.GetFiles(testDirectoryPath, "*.tmp"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task LoadRecoversPreviousGenerationWhenNewestIsCorruptAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(
                CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName(
                    "이전 자료구조"),
                CancellationToken.None);
            await store.SaveAsync(
                CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName(
                    "손상될 자료구조"),
                CancellationToken.None);
            await File.WriteAllBytesAsync(
                getGenerationPath(testDirectoryPath, 2L),
                new byte[] { 0x01, 0x02, 0x03 },
                CancellationToken.None);

            CatalogCacheLoadResult result = await store.LoadAsync(CancellationToken.None);

            Assert.AreEqual(
                ECatalogCacheLoadStatus.RecoveredPreviousGeneration,
                result.Status);
            Assert.AreEqual(
                "이전 자료구조",
                result.GetPackage().Document.Catalog.Courses[0].KoreanName.Value);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SaveDoesNotCreateGenerationForIdenticalVerifiedPackageAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            VerifiedCatalogPackage package =
                CatalogSynchronizationTestDocuments.CreateVerifiedPackage();
            await store.SaveAsync(package, CancellationToken.None);
            byte[] firstGenerationBefore = await File.ReadAllBytesAsync(
                getGenerationPath(testDirectoryPath, 1L),
                CancellationToken.None);

            await store.SaveAsync(package, CancellationToken.None);
            byte[] firstGenerationAfter = await File.ReadAllBytesAsync(
                getGenerationPath(testDirectoryPath, 1L),
                CancellationToken.None);

            CollectionAssert.AreEqual(firstGenerationBefore, firstGenerationAfter);
            Assert.HasCount(1, Directory.GetFiles(testDirectoryPath, "catalog.g*.cache"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task LoadThrowsWhenEveryGenerationIsCorruptAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            Directory.CreateDirectory(testDirectoryPath);
            await File.WriteAllBytesAsync(
                getGenerationPath(testDirectoryPath, 1L),
                new byte[] { 0x01 },
                CancellationToken.None);
            await File.WriteAllBytesAsync(
                getGenerationPath(testDirectoryPath, 2L),
                new byte[] { 0x02 },
                CancellationToken.None);
            CatalogCacheFileStore store = createStore(testDirectoryPath);

            await Assert.ThrowsExactlyAsync<CatalogCachePersistenceException>(
                () => store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task FutureSchemaBlocksLoadAndSaveWithoutChangingCacheAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(
                CatalogSynchronizationTestDocuments.CreateVerifiedPackage(),
                CancellationToken.None);
            string generationPath = getGenerationPath(testDirectoryPath, 1L);
            await writeSchemaVersionAsync(generationPath, 2);
            byte[][] contentBeforeSave = await readGenerationContentsAsync(
                testDirectoryPath);

            CatalogCacheUpgradeRequiredException loadException =
                await Assert.ThrowsExactlyAsync<CatalogCacheUpgradeRequiredException>(
                    () => store.LoadAsync(CancellationToken.None));
            CatalogCacheUpgradeRequiredException saveException =
                await Assert.ThrowsExactlyAsync<CatalogCacheUpgradeRequiredException>(
                    () => store.SaveAsync(
                        CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName(
                            "덮어쓰면 안 되는 자료구조"),
                        CancellationToken.None));
            byte[][] contentAfterSave = await readGenerationContentsAsync(
                testDirectoryPath);

            Assert.AreEqual(2, loadException.UnsupportedSchemaVersion);
            Assert.AreEqual(2, saveException.UnsupportedSchemaVersion);
            assertGenerationContentsEqual(contentBeforeSave, contentAfterSave);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task CorruptNewestGenerationCannotHideFutureSchemaDuringSaveAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            await store.SaveAsync(
                CatalogSynchronizationTestDocuments.CreateVerifiedPackage(),
                CancellationToken.None);
            await writeSchemaVersionAsync(
                getGenerationPath(testDirectoryPath, 1L),
                2);
            await File.WriteAllBytesAsync(
                getGenerationPath(testDirectoryPath, 2L),
                new byte[] { 0x01, 0x02, 0x03 },
                CancellationToken.None);
            byte[][] contentBeforeSave = await readGenerationContentsAsync(
                testDirectoryPath);

            await Assert.ThrowsExactlyAsync<CatalogCacheUpgradeRequiredException>(
                () => store.LoadAsync(CancellationToken.None));
            await Assert.ThrowsExactlyAsync<CatalogCacheUpgradeRequiredException>(
                () => store.SaveAsync(
                    CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName(
                        "미래 캐시를 숨기면 안 되는 자료구조"),
                    CancellationToken.None));
            byte[][] contentAfterSave = await readGenerationContentsAsync(
                testDirectoryPath);

            assertGenerationContentsEqual(contentBeforeSave, contentAfterSave);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SaveRetainsFiveNewestImmutableGenerationsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            for (int saveCount = 0; saveCount < 7; ++saveCount)
            {
                VerifiedCatalogPackage package =
                    CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName(
                        "자료구조 " + saveCount);
                await store.SaveAsync(package, CancellationToken.None);
            }

            string[] generationPaths = Directory.GetFiles(
                testDirectoryPath,
                "catalog.g*.cache");

            Assert.HasCount(5, generationPaths);
            Assert.IsFalse(File.Exists(getGenerationPath(testDirectoryPath, 2L)));
            Assert.IsTrue(File.Exists(getGenerationPath(testDirectoryPath, 3L)));
            Assert.IsTrue(File.Exists(getGenerationPath(testDirectoryPath, 7L)));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task CanceledLoadDoesNotReturnNotFoundAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            using (CancellationTokenSource cancellationSource =
                new CancellationTokenSource())
            {
                cancellationSource.Cancel();
                bool wasCanceled = false;
                try
                {
                    await store.LoadAsync(cancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                }

                Assert.IsTrue(wasCanceled);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task CanceledSaveDoesNotCreateAGenerationAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogCacheFileStore store = createStore(testDirectoryPath);
            using (CancellationTokenSource cancellationSource =
                new CancellationTokenSource())
            {
                cancellationSource.Cancel();
                bool wasCanceled = false;
                try
                {
                    await store.SaveAsync(
                        CatalogSynchronizationTestDocuments.CreateVerifiedPackage(),
                        cancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                }

                Assert.IsTrue(wasCanceled);
                Assert.IsFalse(Directory.Exists(testDirectoryPath));
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    private static CatalogCacheFileStore createStore(string testDirectoryPath)
    {
        CatalogCacheFilePath cachePath = new CatalogCacheFilePath(
            Path.Combine(testDirectoryPath, "catalog.cache"));
        return new CatalogCacheFileStore(cachePath, createLimits());
    }

    private static CatalogSynchronizationLimits createLimits()
    {
        return new CatalogSynchronizationLimits(
            new CatalogResourceByteLimit(64_000L),
            new CatalogResourceByteLimit(1_000_000L));
    }

    private static string createTestDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static string getGenerationPath(
        string testDirectoryPath,
        long generation)
    {
        string fileName = "catalog.g"
            + generation.ToString("D20", CultureInfo.InvariantCulture)
            + ".cache";
        return Path.Combine(testDirectoryPath, fileName);
    }

    private static async Task writeSchemaVersionAsync(
        string generationPath,
        int schemaVersion)
    {
        byte[] content = await File.ReadAllBytesAsync(
            generationPath,
            CancellationToken.None);
        BinaryPrimitives.WriteInt32LittleEndian(
            content.AsSpan(CACHE_SCHEMA_VERSION_OFFSET, sizeof(int)),
            schemaVersion);
        await File.WriteAllBytesAsync(
            generationPath,
            content,
            CancellationToken.None);
    }

    private static async Task<byte[][]> readGenerationContentsAsync(
        string testDirectoryPath)
    {
        string[] generationPaths = Directory.GetFiles(
            testDirectoryPath,
            "catalog.g*.cache");
        Array.Sort(generationPaths, StringComparer.Ordinal);
        byte[][] contents = new byte[generationPaths.Length][];
        for (int index = 0; index < generationPaths.Length; ++index)
        {
            contents[index] = await File.ReadAllBytesAsync(
                generationPaths[index],
                CancellationToken.None);
        }

        return contents;
    }

    private static void assertGenerationContentsEqual(
        byte[][] expectedContents,
        byte[][] actualContents)
    {
        Assert.HasCount(expectedContents.Length, actualContents);
        for (int index = 0; index < expectedContents.Length; ++index)
        {
            CollectionAssert.AreEqual(expectedContents[index], actualContents[index]);
        }
    }

    private static void deleteTestDirectory(string testDirectoryPath)
    {
        if (Directory.Exists(testDirectoryPath))
        {
            Directory.Delete(testDirectoryPath, true);
        }
    }
}
