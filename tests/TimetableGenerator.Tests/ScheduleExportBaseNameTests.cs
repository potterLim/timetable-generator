using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Infrastructure.Exporting;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class ScheduleExportBaseNameTests
{
    [TestMethod]
    public void ConstructorSanitizesWindowsFileNameCharactersAndReservedNames()
    {
        ScheduleExportBaseName sanitizedName = new ScheduleExportBaseName(
            "  2026: 봄/학기??  ");
        ScheduleExportBaseName reservedName = new ScheduleExportBaseName("con");

        Assert.AreEqual("2026_ 봄_학기_", sanitizedName.Value);
        Assert.AreEqual("con_", reservedName.Value);
    }

    [TestMethod]
    public void ConstructorUsesAProductDefaultForAnEmptyNameAndLimitsLength()
    {
        ScheduleExportBaseName emptyName = new ScheduleExportBaseName("   ...   ");
        ScheduleExportBaseName longName = new ScheduleExportBaseName(new string('가', 120));
        ScheduleExportBaseName emojiBoundaryName = new ScheduleExportBaseName(
            new string('a', 79) + "😀");

        Assert.AreEqual("시간표", emptyName.Value);
        Assert.AreEqual(80, longName.Value.Length);
        Assert.AreEqual(new string('a', 79), emojiBoundaryName.Value);
    }

    [TestMethod]
    public void DirectoryPathNormalizesToAFullyQualifiedPath()
    {
        string relativePath = Path.Combine("exports", Guid.NewGuid().ToString("N"));

        ScheduleExportDirectoryPath directoryPath = new ScheduleExportDirectoryPath(relativePath);

        Assert.IsTrue(directoryPath.IsValid);
        Assert.IsTrue(Path.IsPathFullyQualified(directoryPath.Value));
        Assert.AreEqual(Path.GetFullPath(relativePath), directoryPath.Value);
    }
}
