using System;
using System.Collections.Generic;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed class GenerationFileRetentionSet
{
    private readonly HashSet<FileGeneration> mRetainedGenerations;

    public GenerationFileRetentionSet()
    {
        mRetainedGenerations = new HashSet<FileGeneration>();
    }

    public void Retain(GenerationFile generationFile)
    {
        if (generationFile == null)
        {
            throw new ArgumentNullException(nameof(generationFile));
        }

        mRetainedGenerations.Add(generationFile.Generation);
    }

    public bool ShouldRetain(GenerationFile generationFile)
    {
        if (generationFile == null)
        {
            throw new ArgumentNullException(nameof(generationFile));
        }

        return mRetainedGenerations.Contains(generationFile.Generation);
    }
}
