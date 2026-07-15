using System;
using System.Collections.Generic;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportCleanupException : Exception
{
    private readonly IReadOnlyList<SchedulePngExportArtifact> mRetainedArtifacts;

    public IReadOnlyList<SchedulePngExportArtifact> RetainedArtifacts
    {
        get
        {
            return mRetainedArtifacts;
        }
    }

    internal SchedulePngExportCleanupException(
        IEnumerable<SchedulePngExportArtifact> retainedArtifacts,
        Exception innerException)
        : base(
            "PNG 내보내기가 중단되었고 일부 출력 파일을 정리하지 못했습니다.",
            innerException)
    {
        if (retainedArtifacts == null)
        {
            throw new ArgumentNullException(nameof(retainedArtifacts));
        }

        List<SchedulePngExportArtifact> copiedArtifacts =
            new List<SchedulePngExportArtifact>();
        foreach (SchedulePngExportArtifact retainedArtifact in retainedArtifacts)
        {
            if (retainedArtifact == null)
            {
                throw new ArgumentException(
                    "Cleanup artifacts cannot contain null values.",
                    nameof(retainedArtifacts));
            }

            copiedArtifacts.Add(retainedArtifact);
        }

        if (copiedArtifacts.Count == 0)
        {
            throw new ArgumentException(
                "Cleanup exceptions require at least one retained artifact.",
                nameof(retainedArtifacts));
        }

        mRetainedArtifacts = copiedArtifacts.AsReadOnly();
    }
}
