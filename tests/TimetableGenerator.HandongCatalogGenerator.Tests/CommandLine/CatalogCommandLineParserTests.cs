using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Application;
using TimetableGenerator.HandongCatalogGenerator.Application.Errors;
using TimetableGenerator.HandongCatalogGenerator.CommandLine;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.CommandLine;

[TestClass]
public sealed class CatalogCommandLineParserTests
{
    [TestMethod]
    public void Parse_CompleteGenerateCommand_ReturnsStronglyTypedRequest()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "source.xls");
        string outputRootPath = Path.Combine(Path.GetTempPath(), "catalog-output");

        CatalogGenerationRequest request = CatalogCommandLineParser.Parse(createValidArguments(sourcePath, outputRootPath));

        Assert.AreEqual(Path.GetFullPath(sourcePath), request.SourceFilePath.Value);
        Assert.AreEqual("2026-2", request.Term.Id);
        Assert.AreEqual(7, request.Revision.Value);
        Assert.AreEqual(Path.GetFullPath(outputRootPath), request.OutputRootPath.Value);
    }

    [TestMethod]
    public void Parse_OptionsInDifferentOrder_ReturnsEquivalentRequest()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "source.xls");
        string outputRootPath = Path.Combine(Path.GetTempPath(), "catalog-output");
        string[] arguments = new string[]
        {
            "generate",
            "--output-root",
            outputRootPath,
            "--revision",
            "7",
            "--source",
            sourcePath,
            "--term",
            "2026-2",
        };

        CatalogGenerationRequest request = CatalogCommandLineParser.Parse(arguments);

        Assert.AreEqual(Path.GetFullPath(sourcePath), request.SourceFilePath.Value);
        Assert.AreEqual("2026-2", request.Term.Id);
        Assert.AreEqual(7, request.Revision.Value);
        Assert.AreEqual(Path.GetFullPath(outputRootPath), request.OutputRootPath.Value);
    }

    [TestMethod]
    public void Parse_UnknownOption_ReportsTypedCommandLineError()
    {
        List<string> arguments = createValidArgumentList();
        arguments.Add("--school");
        arguments.Add("handong-global-university");

        CatalogGenerationException exception = Assert.ThrowsExactly<CatalogGenerationException>(() => CatalogCommandLineParser.Parse(arguments));

        assertInvalidArgumentsError(exception, ECatalogGenerationErrorCode.UnknownOption);
    }

    [TestMethod]
    public void Parse_LegacyPublishedAtOption_ReportsUnknownOption()
    {
        List<string> arguments = createValidArgumentList();
        arguments.Add("--published-at");
        arguments.Add("2026-07-16T00:00:00Z");

        CatalogGenerationException exception = Assert.ThrowsExactly<CatalogGenerationException>(() => CatalogCommandLineParser.Parse(arguments));

        assertInvalidArgumentsError(exception, ECatalogGenerationErrorCode.UnknownOption);
    }

    [TestMethod]
    public void Parse_DuplicateOption_ReportsTypedCommandLineError()
    {
        List<string> arguments = createValidArgumentList();
        arguments.Add("--revision");
        arguments.Add("8");

        CatalogGenerationException exception = Assert.ThrowsExactly<CatalogGenerationException>(() => CatalogCommandLineParser.Parse(arguments));

        assertInvalidArgumentsError(exception, ECatalogGenerationErrorCode.DuplicateOption);
    }

    [TestMethod]
    public void Parse_MissingRequiredOption_ReportsAllMissingOptions()
    {
        string[] arguments = new string[]
        {
            "generate",
            "--source",
            "source.xls",
        };

        CatalogGenerationException exception = Assert.ThrowsExactly<CatalogGenerationException>(() => CatalogCommandLineParser.Parse(arguments));

        assertInvalidArgumentsError(exception, ECatalogGenerationErrorCode.MissingRequiredOption);
        StringAssert.Contains(exception.Message, "--term");
        StringAssert.Contains(exception.Message, "--revision");
        StringAssert.Contains(exception.Message, "--output-root");
    }

    [TestMethod]
    public void Parse_OptionWithoutValue_ReportsTypedCommandLineError()
    {
        string[] arguments = new string[]
        {
            "generate",
            "--source",
            "--term",
            "2026-2",
        };

        CatalogGenerationException exception = Assert.ThrowsExactly<CatalogGenerationException>(() => CatalogCommandLineParser.Parse(arguments));

        assertInvalidArgumentsError(exception, ECatalogGenerationErrorCode.MissingOptionValue);
        StringAssert.Contains(exception.Message, "--source");
    }

    [TestMethod]
    [DataRow("--term", "2026-02")]
    [DataRow("--revision", "0")]
    [DataRow("--revision", "1.5")]
    public void Parse_InvalidOptionValue_ReportsTypedCommandLineError(string optionName, string invalidValue)
    {
        List<string> arguments = createValidArgumentList();
        int optionIndex = arguments.IndexOf(optionName);
        arguments[optionIndex + 1] = invalidValue;

        CatalogGenerationException exception = Assert.ThrowsExactly<CatalogGenerationException>(() => CatalogCommandLineParser.Parse(arguments));

        assertInvalidArgumentsError(exception, ECatalogGenerationErrorCode.InvalidOptionValue);
        StringAssert.Contains(exception.Message, optionName);
    }

    private static string[] createValidArguments(string sourcePath, string outputRootPath)
    {
        return new string[]
        {
            "generate",
            "--source",
            sourcePath,
            "--term",
            "2026-2",
            "--revision",
            "7",
            "--output-root",
            outputRootPath,
        };
    }

    private static List<string> createValidArgumentList()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "source.xls");
        string outputRootPath = Path.Combine(Path.GetTempPath(), "catalog-output");
        return new List<string>(createValidArguments(sourcePath, outputRootPath));
    }

    private static void assertInvalidArgumentsError(CatalogGenerationException exception, ECatalogGenerationErrorCode expectedErrorCode)
    {
        Assert.AreEqual(expectedErrorCode, exception.ErrorCode);
        Assert.AreEqual(ECatalogGeneratorExitCode.InvalidArguments, exception.ExitCode);
    }
}
