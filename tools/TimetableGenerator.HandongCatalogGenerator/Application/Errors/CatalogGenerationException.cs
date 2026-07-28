using System;

namespace TimetableGenerator.HandongCatalogGenerator.Application.Errors;

internal sealed class CatalogGenerationException : Exception
{
    public ECatalogGenerationErrorCode ErrorCode { get; }
    public ECatalogGeneratorExitCode ExitCode { get; }

    public CatalogGenerationException(ECatalogGenerationErrorCode errorCode, ECatalogGeneratorExitCode exitCode, string message)
        : this(errorCode, exitCode, message, null)
    {
    }

    public CatalogGenerationException(ECatalogGenerationErrorCode errorCode, ECatalogGeneratorExitCode exitCode, string message, Exception? innerExceptionOrNull)
        : base(message, innerExceptionOrNull)
    {
        if (errorCode == ECatalogGenerationErrorCode.None)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "A catalog generation exception must have an error code.");
        }

        if (exitCode == ECatalogGeneratorExitCode.Succeeded)
        {
            throw new ArgumentOutOfRangeException(nameof(exitCode), exitCode, "An exception cannot use the successful exit code.");
        }

        ErrorCode = errorCode;
        ExitCode = exitCode;
    }
}
