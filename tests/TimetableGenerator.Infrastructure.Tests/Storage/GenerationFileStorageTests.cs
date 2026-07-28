using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Infrastructure.Storage;

namespace TimetableGenerator.Infrastructure.Tests.Storage;

[TestClass]
public sealed class GenerationFileStorageTests
{
    [TestMethod]
    public async Task CommitCreatesAnImmutableGenerationWithoutTemporaryFilesAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            GenerationFileStorage storage = createStorage(testDirectoryPath);
            byte[] content = Encoding.UTF8.GetBytes("durable content");

            using (GenerationFileStorageAccess storageAccess = await storage.AcquireCreatingDirectoryAsync(CancellationToken.None))
            {
                GenerationFile generationFile = await storage.CommitAsync(new FileGeneration(1L), content, CancellationToken.None);

                byte[] committedContent = await File.ReadAllBytesAsync(generationFile.Path.Value, CancellationToken.None);
                CollectionAssert.AreEqual(content, committedContent);
                Assert.IsEmpty(Directory.GetFiles(testDirectoryPath, "*.tmp"));
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task FailedDuplicateCommitRemovesItsTemporaryFileAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            GenerationFileStorage storage = createStorage(testDirectoryPath);
            using (GenerationFileStorageAccess storageAccess = await storage.AcquireCreatingDirectoryAsync(CancellationToken.None))
            {
                FileGeneration generation = new FileGeneration(1L);
                await storage.CommitAsync(generation, new byte[] { 0x01 }, CancellationToken.None);

                await Assert.ThrowsExactlyAsync<IOException>(
                    () => storage.CommitAsync(
                        generation,
                        new byte[] { 0x02 },
                        CancellationToken.None));

                Assert.IsEmpty(Directory.GetFiles(testDirectoryPath, "*.tmp"));
                byte[] committedContent = await File.ReadAllBytesAsync(getGenerationPath(testDirectoryPath, generation), CancellationToken.None);
                CollectionAssert.AreEqual(new byte[] { 0x01 }, committedContent);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task DiscoveryReturnsOnlyTypedGenerationsInNewestFirstOrderAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            Directory.CreateDirectory(testDirectoryPath);
            await File.WriteAllTextAsync(getGenerationPath(testDirectoryPath, new FileGeneration(1L)), "first", CancellationToken.None);
            await File.WriteAllTextAsync(getGenerationPath(testDirectoryPath, new FileGeneration(3L)), "third", CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(testDirectoryPath, "product.g3.data"), "invalid width", CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(testDirectoryPath, "other.g00000000000000000002.data"), "other product", CancellationToken.None);
            GenerationFileStorage storage = createStorage(testDirectoryPath);

            IReadOnlyList<GenerationFile> generationFiles = storage.GetGenerationFiles();

            Assert.HasCount(2, generationFiles);
            Assert.AreEqual(3L, generationFiles[0].Generation.Value);
            Assert.AreEqual(1L, generationFiles[1].Generation.Value);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task PruningRetainsTheRecoveryWindowAndAnAdditionalGenerationAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            GenerationFileStorage storage = createStorage(testDirectoryPath);
            using (GenerationFileStorageAccess storageAccess = await storage.AcquireCreatingDirectoryAsync(CancellationToken.None))
            {
                GenerationFile? firstGenerationOrNull = null;
                for (long generationValue = 1L; generationValue <= 7L; ++generationValue)
                {
                    GenerationFile generationFile = await storage.CommitAsync(new FileGeneration(generationValue), new byte[] { checked((byte)generationValue) }, CancellationToken.None);
                    if (generationValue == 1L)
                    {
                        firstGenerationOrNull = generationFile;
                    }
                }

                Assert.IsNotNull(firstGenerationOrNull);
                GenerationFileRetentionSet retentionSet = new GenerationFileRetentionSet();
                retentionSet.Retain(firstGenerationOrNull);

                storage.PruneGenerations(retentionSet);

                Assert.IsTrue(File.Exists(getGenerationPath(testDirectoryPath, new FileGeneration(1L))));
                Assert.IsFalse(File.Exists(getGenerationPath(testDirectoryPath, new FileGeneration(2L))));
                Assert.HasCount(6, Directory.GetFiles(testDirectoryPath, "product.g*.data"));
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task CancelledCrossProcessLockAttemptAllowsASecondAttemptAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            GenerationFileStorage firstStorage = createStorage(testDirectoryPath);
            GenerationFileStorage secondStorage = createStorage(testDirectoryPath);
            using (GenerationFileStorageAccess firstAccess = await firstStorage.AcquireCreatingDirectoryAsync(CancellationToken.None))
            {
                using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
                {
                    cancellationSource.CancelAfter(100);
                    await Assert.ThrowsAsync<OperationCanceledException>(
                        () => secondStorage.AcquireExistingDirectoryAsync(
                            cancellationSource.Token));
                }
            }

            using (GenerationFileStorageAccess secondAccess = await secondStorage.AcquireExistingDirectoryAsync(CancellationToken.None))
            {
                Assert.IsNotNull(secondAccess);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    private static GenerationFileStorage createStorage(string testDirectoryPath)
    {
        string baseFilePath = Path.Combine(testDirectoryPath, "product.data");
        GenerationFileStoragePath storagePath = new GenerationFileStoragePath(baseFilePath);
        return new GenerationFileStorage(storagePath);
    }

    private static string getGenerationPath(string testDirectoryPath, FileGeneration generation)
    {
        return Path.Combine(testDirectoryPath, "product." + generation.FileComponent + ".data");
    }

    private static string createTestDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "TimetableGenerator.Infrastructure.Tests", Guid.NewGuid().ToString("N"));
    }

    private static void deleteTestDirectory(string testDirectoryPath)
    {
        if (Directory.Exists(testDirectoryPath))
        {
            Directory.Delete(testDirectoryPath, true);
        }
    }
}
