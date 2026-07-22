using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TimetableGenerator.HandongCatalogGenerator.Application;
using TimetableGenerator.HandongCatalogGenerator.Application.Errors;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.CommandLine;

internal static class CatalogCommandLineParser
{
    private const string GENERATE_COMMAND = "generate";
    private const string OUTPUT_ROOT_OPTION = "--output-root";
    private const string REVISION_OPTION = "--revision";
    private const string SOURCE_OPTION = "--source";
    private const string TERM_OPTION = "--term";

    public const string USAGE = "generate --source <path> --term <YYYY-S> --revision <positive-int> " + "--output-root <path>";

    public static CatalogGenerationRequest Parse(IReadOnlyList<string> arguments)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        validateCommand(arguments);
        Dictionary<string, string> optionValues = readOptionValues(arguments);
        validateRequiredOptions(optionValues);

        return new CatalogGenerationRequest(
            parseSourceFilePath(optionValues[SOURCE_OPTION]),
            parseAcademicTerm(optionValues[TERM_OPTION]),
            parseRevision(optionValues[REVISION_OPTION]),
            parseOutputRootPath(optionValues[OUTPUT_ROOT_OPTION]));
    }

    private static void validateCommand(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 ||
            string.Equals(arguments[0], GENERATE_COMMAND, StringComparison.Ordinal) == false)
        {
            string suppliedCommand = arguments.Count == 0 ? "<missing>" : arguments[0];
            throw createCommandLineException(
                ECatalogGenerationErrorCode.InvalidCommand,
                "Expected the 'generate' command but received '" + suppliedCommand + "'.");
        }
    }

    private static Dictionary<string, string> readOptionValues(IReadOnlyList<string> arguments)
    {
        Dictionary<string, string> optionValues = new Dictionary<string, string>(StringComparer.Ordinal);
        int argumentIndex = 1;
        while (argumentIndex < arguments.Count)
        {
            string optionName = arguments[argumentIndex];
            if (isKnownOption(optionName) == false)
            {
                throw createCommandLineException(
                    ECatalogGenerationErrorCode.UnknownOption,
                    "Unknown option '" + optionName + "'.");
            }

            if (optionValues.ContainsKey(optionName))
            {
                throw createCommandLineException(
                    ECatalogGenerationErrorCode.DuplicateOption,
                    "Option '" + optionName + "' can be specified only once.");
            }

            int valueIndex = argumentIndex + 1;
            if (valueIndex >= arguments.Count || isOptionToken(arguments[valueIndex]))
            {
                throw createCommandLineException(
                    ECatalogGenerationErrorCode.MissingOptionValue,
                    "Option '" + optionName + "' requires a value.");
            }

            optionValues.Add(optionName, arguments[valueIndex]);
            argumentIndex += 2;
        }

        return optionValues;
    }

    private static void validateRequiredOptions(IReadOnlyDictionary<string, string> optionValues)
    {
        List<string> missingOptions = new List<string>();
        addMissingOption(optionValues, missingOptions, SOURCE_OPTION);
        addMissingOption(optionValues, missingOptions, TERM_OPTION);
        addMissingOption(optionValues, missingOptions, REVISION_OPTION);
        addMissingOption(optionValues, missingOptions, OUTPUT_ROOT_OPTION);
        if (missingOptions.Count > 0)
        {
            throw createCommandLineException(
                ECatalogGenerationErrorCode.MissingRequiredOption,
                "Missing required option(s): " + string.Join(", ", missingOptions) + ".");
        }
    }

    private static void addMissingOption(
        IReadOnlyDictionary<string, string> optionValues,
        ICollection<string> missingOptions,
        string optionName)
    {
        if (optionValues.ContainsKey(optionName) == false)
        {
            missingOptions.Add(optionName);
        }
    }

    private static bool isKnownOption(string value)
    {
        return string.Equals(value, SOURCE_OPTION, StringComparison.Ordinal) ||
            string.Equals(value, TERM_OPTION, StringComparison.Ordinal) ||
            string.Equals(value, REVISION_OPTION, StringComparison.Ordinal) ||
            string.Equals(value, OUTPUT_ROOT_OPTION, StringComparison.Ordinal);
    }

    private static bool isOptionToken(string value)
    {
        return value.StartsWith("--", StringComparison.Ordinal);
    }

    private static CatalogSourceFilePath parseSourceFilePath(string value)
    {
        try
        {
            return new CatalogSourceFilePath(value);
        }
        catch (Exception exception) when (isPathValidationException(exception))
        {
            throw createInvalidOptionValueException(SOURCE_OPTION, exception.Message, exception);
        }
    }

    private static AcademicTerm parseAcademicTerm(string value)
    {
        if (hasExactAcademicTermFormat(value) == false)
        {
            throw createInvalidOptionValueException(
                TERM_OPTION,
                "The academic term must use the YYYY-S format with semester 1 or 2.");
        }

        try
        {
            return AcademicTerm.Parse(value);
        }
        catch (Exception exception) when (
            exception is FormatException || exception is ArgumentOutOfRangeException)
        {
            throw createInvalidOptionValueException(TERM_OPTION, exception.Message, exception);
        }
    }

    private static bool hasExactAcademicTermFormat(string value)
    {
        return value.Length == 6 &&
            isAsciiDigit(value[0]) &&
            isAsciiDigit(value[1]) &&
            isAsciiDigit(value[2]) &&
            isAsciiDigit(value[3]) &&
            value[4] == '-' &&
            (value[5] == '1' || value[5] == '2');
    }

    private static bool isAsciiDigit(char value)
    {
        return value >= '0' && value <= '9';
    }

    private static CatalogRevision parseRevision(string value)
    {
        int revisionValue;
        bool isParsed = int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out revisionValue);
        if (isParsed == false)
        {
            throw createInvalidOptionValueException(
                REVISION_OPTION,
                "The revision must be a positive integer.");
        }

        try
        {
            return new CatalogRevision(revisionValue);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw createInvalidOptionValueException(REVISION_OPTION, exception.Message, exception);
        }
    }

    private static CatalogOutputRootPath parseOutputRootPath(string value)
    {
        try
        {
            return new CatalogOutputRootPath(value);
        }
        catch (Exception exception) when (isPathValidationException(exception))
        {
            throw createInvalidOptionValueException(OUTPUT_ROOT_OPTION, exception.Message, exception);
        }
    }

    private static bool isPathValidationException(Exception exception)
    {
        return exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException;
    }

    private static CatalogGenerationException createInvalidOptionValueException(
        string optionName,
        string reason,
        Exception? innerExceptionOrNull = null)
    {
        return new CatalogGenerationException(
            ECatalogGenerationErrorCode.InvalidOptionValue,
            ECatalogGeneratorExitCode.InvalidArguments,
            "Invalid value for option '" + optionName + "': " + reason,
            innerExceptionOrNull);
    }

    private static CatalogGenerationException createCommandLineException(
        ECatalogGenerationErrorCode errorCode,
        string message)
    {
        return new CatalogGenerationException(
            errorCode,
            ECatalogGeneratorExitCode.InvalidArguments,
            message);
    }
}
