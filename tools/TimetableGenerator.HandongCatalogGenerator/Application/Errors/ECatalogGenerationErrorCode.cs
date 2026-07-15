namespace TimetableGenerator.HandongCatalogGenerator.Application.Errors;

internal enum ECatalogGenerationErrorCode
{
    None = 0,
    InvalidCommand,
    UnknownOption,
    DuplicateOption,
    MissingRequiredOption,
    MissingOptionValue,
    InvalidOptionValue,
    SourceFileNotFound,
    SourceReadFailed,
    UnsupportedSourceFormat,
    InvalidSourceEncoding,
    SourceSchemaMismatch,
    TermMismatch,
    InvalidSourceRecord,
    DuplicateOffering,
    ConflictingCourseDefinition,
    CatalogSerializationFailed,
    InvalidExistingIndex,
    OutputConflict,
    OutputWriteFailed,
    UnexpectedFailure,
}
