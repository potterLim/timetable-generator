using System;
using System.Diagnostics;
using System.IO;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed record GenerationFileStoragePath
{
    public string DirectoryPath { get; }

    public string BaseFileName { get; }

    public string FileExtension { get; }

    public string GenerationSearchPattern
    {
        get
        {
            return BaseFileName + ".g*" + FileExtension;
        }
    }

    public string TemporaryFileSearchPattern
    {
        get
        {
            return BaseFileName + ".*.tmp";
        }
    }

    public string LockPath
    {
        get
        {
            return Path.Combine(DirectoryPath, BaseFileName + ".lock");
        }
    }

    public GenerationFileStoragePath(string baseFilePath)
    {
        if (baseFilePath == null)
        {
            throw new ArgumentNullException(nameof(baseFilePath));
        }

        string? directoryPathOrNull = Path.GetDirectoryName(baseFilePath);
        Debug.Assert(directoryPathOrNull != null);
        if (directoryPathOrNull == null)
        {
            throw new ArgumentException(
                "Generation file paths must include a directory.",
                nameof(baseFilePath));
        }

        DirectoryPath = directoryPathOrNull;
        BaseFileName = Path.GetFileNameWithoutExtension(baseFilePath);
        FileExtension = Path.GetExtension(baseFilePath);
    }

    public GenerationFilePath CreateGenerationFilePath(FileGeneration generation)
    {
        if (generation.IsValid == false)
        {
            throw new ArgumentException(
                "Generation file paths require a valid generation.",
                nameof(generation));
        }

        string fileName = BaseFileName
            + "."
            + generation.FileComponent
            + FileExtension;
        return new GenerationFilePath(Path.Combine(DirectoryPath, fileName));
    }

    public bool TryParseGenerationFilePath(string path, out FileGeneration generation)
    {
        generation = default(FileGeneration);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string expectedPrefix = BaseFileName + ".";
        if (fileNameWithoutExtension.StartsWith(expectedPrefix, StringComparison.Ordinal) == false)
        {
            return false;
        }

        string generationComponent = fileNameWithoutExtension.Substring(expectedPrefix.Length);
        return FileGeneration.TryParseFileComponent(generationComponent, out generation);
    }
}
