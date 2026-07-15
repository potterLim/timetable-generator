namespace TimetableGenerator.HandongCatalogGenerator.Application.Errors;

internal enum ECatalogGeneratorExitCode
{
    Succeeded = 0,
    UnexpectedFailure = 1,
    InvalidArguments = 2,
    SourceFailure = 3,
    DataValidationFailed = 4,
    OutputFailure = 5,
}
