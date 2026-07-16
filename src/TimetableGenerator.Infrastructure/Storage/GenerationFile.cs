using System;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed class GenerationFile
{
    public FileGeneration Generation { get; }

    public GenerationFilePath Path { get; }

    public GenerationFile(
        FileGeneration generation,
        GenerationFilePath path)
    {
        if (generation.IsValid == false)
        {
            throw new ArgumentException(
                "Generation files require a valid generation.",
                nameof(generation));
        }

        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        Generation = generation;
        Path = path;
    }
}
