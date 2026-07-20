using System;
using System.Text;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed class SchedulePngFileNameFactoryTests
{
    [Fact]
    public void FileNameUsesOnlyTheActivePlanName()
    {
        PlanName planName = new PlanName("2026-2학기 시간표");

        string fileName = SchedulePngFileNameFactory.Create(planName);

        Assert.Equal("2026-2학기 시간표.png", fileName);
    }

    [Fact]
    public void FileNameReplacesCharactersThatAreUnsafeAcrossDesktopPlatforms()
    {
        PlanName planName = new PlanName("공강/실습:안");

        string fileName = SchedulePngFileNameFactory.Create(planName);

        Assert.Equal("공강-실습-안.png", fileName);
    }

    [Theory]
    [InlineData("CON", "CON-.png")]
    [InlineData("NUL.txt", "NUL-.txt.png")]
    [InlineData("COM¹", "COM¹-.png")]
    [InlineData("COM¹.txt", "COM¹-.txt.png")]
    [InlineData("COM²", "COM²-.png")]
    [InlineData("COM².txt", "COM²-.txt.png")]
    [InlineData("COM³", "COM³-.png")]
    [InlineData("COM³.txt", "COM³-.txt.png")]
    [InlineData("LPT¹", "LPT¹-.png")]
    [InlineData("LPT¹.txt", "LPT¹-.txt.png")]
    [InlineData("LPT²", "LPT²-.png")]
    [InlineData("LPT².txt", "LPT²-.txt.png")]
    [InlineData("LPT³", "LPT³-.png")]
    [InlineData("LPT³.txt", "LPT³-.txt.png")]
    [InlineData("일정.", "일정.png")]
    public void FileNameAvoidsWindowsReservedOrTrailingValues(
        string planNameValue,
        string expectedFileName)
    {
        string fileName = SchedulePngFileNameFactory.Create(
            new PlanName(planNameValue));

        Assert.Equal(expectedFileName, fileName);
    }

    [Fact]
    public void MissingPlanNameUsesTheFallbackFileName()
    {
        string fileName = SchedulePngFileNameFactory.Create(null);

        Assert.Equal("시간표.png", fileName);
    }

    [Fact]
    public void DotOnlyPlanNameUsesTheFallbackFileName()
    {
        string fileName = SchedulePngFileNameFactory.Create(
            new PlanName("..."));

        Assert.Equal("시간표.png", fileName);
    }

    [Fact]
    public void BatchFolderNameUsesTheSanitizedPlanName()
    {
        PlanName planName = new PlanName("2026-2/야간");

        string folderName =
            SchedulePngFileNameFactory.CreateBatchFolderName(planName);

        Assert.Equal("2026-2-야간 - 가능한 시간표", folderName);
    }

    [Theory]
    [InlineData(1, 4, "2026-2학기 시간표 (1).png")]
    [InlineData(1, 24, "2026-2학기 시간표 (01).png")]
    [InlineData(24, 24, "2026-2학기 시간표 (24).png")]
    public void BatchCandidateFileNameSortsInRecommendationOrder(
        int value,
        int total,
        string expectedFileName)
    {
        SchedulePngCandidateNumber candidateNumber =
            new SchedulePngCandidateNumber(value, total);

        string fileName = SchedulePngFileNameFactory.CreateBatchCandidate(
            new PlanName("2026-2학기 시간표"),
            candidateNumber);

        Assert.Equal(expectedFileName, fileName);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    public void BatchCandidateNumberRejectsValuesOutsideTheBatch(
        int value,
        int total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                _ = new SchedulePngCandidateNumber(value, total);
            });
    }

    [Fact]
    public void LongKoreanPlanNameFitsMacOsFileSystemComponentLimit()
    {
        PlanName planName = new PlanName(new string('한', 80));

        string folderName =
            SchedulePngFileNameFactory.CreateBatchFolderName(planName);
        string candidateFileName =
            SchedulePngFileNameFactory.CreateBatchCandidate(
                planName,
                new SchedulePngCandidateNumber(24, 24));

        Assert.True(Encoding.UTF8.GetByteCount(folderName) <= 255);
        Assert.True(Encoding.UTF8.GetByteCount(candidateFileName) <= 255);
        Assert.EndsWith(" - 가능한 시간표", folderName, StringComparison.Ordinal);
        Assert.EndsWith(" (24).png", candidateFileName, StringComparison.Ordinal);
    }

    [Fact]
    public void LongBatchFolderCopyNameIncludesSuffixWithinComponentLimit()
    {
        PlanName planName = new PlanName(new string('한', 80));

        string folderName =
            SchedulePngFileNameFactory.CreateBatchFolderName(planName, 2);

        Assert.True(Encoding.UTF8.GetByteCount(folderName) <= 255);
        Assert.EndsWith(
            " - 가능한 시간표 (2)",
            folderName,
            StringComparison.Ordinal);
    }
}
