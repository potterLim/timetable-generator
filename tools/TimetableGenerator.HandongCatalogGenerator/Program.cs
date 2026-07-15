using System;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.HandongCatalogGenerator.Application;
using TimetableGenerator.HandongCatalogGenerator.Application.Errors;
using TimetableGenerator.HandongCatalogGenerator.CommandLine;

namespace TimetableGenerator.HandongCatalogGenerator;

internal static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        try
        {
            CatalogGenerationRequest request = CatalogCommandLineParser.Parse(arguments);
            CatalogGenerationService generationService = new CatalogGenerationService();
            CatalogGenerationResult result = await generationService.GenerateAsync(
                request,
                CancellationToken.None);

            writeResult(result);
            return (int)ECatalogGeneratorExitCode.Succeeded;
        }
        catch (CatalogGenerationException exception)
        {
            Console.Error.WriteLine("[" + exception.ErrorCode + "] " + exception.Message);
            if (exception.ExitCode == ECatalogGeneratorExitCode.InvalidArguments)
            {
                Console.Error.WriteLine("Usage: " + CatalogCommandLineParser.USAGE);
            }

            return (int)exception.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "[" + ECatalogGenerationErrorCode.UnexpectedFailure + "] " +
                "Catalog generation failed unexpectedly: " + exception.Message);
            return (int)ECatalogGeneratorExitCode.UnexpectedFailure;
        }
    }

    private static void writeResult(CatalogGenerationResult result)
    {
        Console.Out.WriteLine("Catalog generation completed successfully.");
        Console.Out.WriteLine("catalogPath: " + result.CatalogPath.Value);
        Console.Out.WriteLine("catalogSizeBytes: " + result.CatalogFileSize.Value);
        Console.Out.WriteLine("catalogSha256: " + result.CatalogSha256.HexValue);
        Console.Out.WriteLine("indexPath: " + result.IndexPath.Value);
        Console.Out.WriteLine("indexSizeBytes: " + result.IndexFileSize.Value);
        Console.Out.WriteLine("sourceSha256: " + result.SourceSha256.HexValue);
        Console.Out.WriteLine("courses: " + result.Summary.CourseCount.Value);
        Console.Out.WriteLine("offerings: " + result.Summary.OfferingCount.Value);
        Console.Out.WriteLine(
            "scheduledOfferings: " + result.Summary.ScheduledOfferingCount.Value);
        Console.Out.WriteLine(
            "meetingNotProvided: " + result.Summary.MeetingNotProvidedCount.Value);
        Console.Out.WriteLine(
            "roomNotProvided: " + result.Summary.RoomNotProvidedCount.Value);
        Console.Out.WriteLine(
            "instructorUnconfirmed: " + result.Summary.InstructorUnconfirmedCount.Value);
        Console.Out.WriteLine(
            "sourceEnglishScheduleMismatch: "
            + result.Summary.EnglishScheduleMismatchCount.Value);
    }
}
