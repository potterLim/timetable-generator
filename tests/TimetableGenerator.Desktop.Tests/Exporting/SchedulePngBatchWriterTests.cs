using System;
using System.IO;
using System.Threading;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed partial class SchedulePngBatchWriterTests
{
    [Fact]
    public void AtomicBatchCommitNeverReusesAnExistingFolder()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(parentDirectoryPath, "2026-2학기 시간표"));
            Directory.CreateDirectory(Path.Combine(parentDirectoryPath, "2026-2학기 시간표 (2)"));

            PlanName planName = new PlanName("2026-2학기 시간표");
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                directory.commitAsUniqueBatch(planName, CancellationToken.None);
                Assert.Equal("2026-2학기 시간표 (3)", Path.GetFileName(directory.DirectoryPath));
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void AtomicBatchCommitSkipsAnExistingFile()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(parentDirectoryPath, "2026-2학기 시간표"), "preserve");

            PlanName planName = new PlanName("2026-2학기 시간표");
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                directory.commitAsUniqueBatch(planName, CancellationToken.None);
                Assert.Equal("2026-2학기 시간표 (2)", Path.GetFileName(directory.DirectoryPath));
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void AtomicBatchCommitKeepsCopySuffixWithinComponentLimit()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            PlanName planName = new PlanName(new string('한', 80));
            string firstFolderName = SchedulePngFileNameFactory.CreateBatchFolderName(planName);
            Directory.CreateDirectory(Path.Combine(parentDirectoryPath, firstFolderName));

            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                directory.commitAsUniqueBatch(planName, CancellationToken.None);
                string folderName = Path.GetFileName(directory.DirectoryPath);
                Assert.True(System.Text.Encoding.UTF8.GetByteCount(folderName) <= 255);
                Assert.EndsWith(" (2)", folderName, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void BatchDirectoryNeverOverwritesAnExistingFile()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                Assert.Throws<IOException>(
                    delegate
                    {
                        using (Stream stream = directory.createFile("후보.png"))
                        {
                        }
                    });
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void UncommittedBatchDirectoryRemovesOnlyItsOwnFiles()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        string directoryPath;
        try
        {
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                directoryPath = directory.DirectoryPath;
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                File.WriteAllText(Path.Combine(directoryPath, "외부 파일.txt"), "preserve");
            }

            Assert.True(Directory.Exists(directoryPath));
            Assert.False(File.Exists(Path.Combine(directoryPath, "후보.png")));
            Assert.True(File.Exists(Path.Combine(directoryPath, "외부 파일.txt")));
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void StagedBatchBecomesVisibleOnlyAfterItsAtomicCommit()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            PlanName planName = new PlanName("2026-2학기 시간표");
            string stagingDirectoryPath;
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                stagingDirectoryPath = directory.DirectoryPath;
                Assert.StartsWith(".timetable-generator-png-staging-", Path.GetFileName(stagingDirectoryPath), StringComparison.Ordinal);
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                string finalDirectoryPath = Path.Combine(parentDirectoryPath, SchedulePngFileNameFactory.CreateBatchFolderName(planName));
                Assert.False(Directory.Exists(finalDirectoryPath));

                directory.commitAsUniqueBatch(planName, CancellationToken.None);

                Assert.False(Directory.Exists(stagingDirectoryPath));
                Assert.Equal(finalDirectoryPath, directory.DirectoryPath);
                Assert.True(Directory.Exists(finalDirectoryPath));
                Assert.True(File.Exists(Path.Combine(finalDirectoryPath, "후보.png")));
                Assert.False(File.Exists(Path.Combine(finalDirectoryPath, ".timetable-generator-exporting")));
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void UncommittedStagingDirectoryRemovesEveryPartialResult()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        string stagingDirectoryPath;
        try
        {
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                stagingDirectoryPath = directory.DirectoryPath;
                using (Stream stream = directory.createFile("부분 결과.png"))
                {
                    stream.WriteByte(1);
                }

                Assert.True(Directory.Exists(stagingDirectoryPath));
            }

            Assert.False(Directory.Exists(stagingDirectoryPath));
            Assert.Empty(Directory.GetFileSystemEntries(parentDirectoryPath));
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }
}
