using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Persistence;

public readonly record struct WorkspaceDocumentSizeLimit
{
    private const long PRODUCT_DEFAULT_BYTES = 8L * 1_024L * 1_024L;

    public long Bytes { get; }

    public bool IsValid
    {
        get
        {
            return Bytes > 0;
        }
    }

    public static WorkspaceDocumentSizeLimit ProductDefault
    {
        get
        {
            return new WorkspaceDocumentSizeLimit(PRODUCT_DEFAULT_BYTES);
        }
    }

    public WorkspaceDocumentSizeLimit(long bytes)
    {
        if (bytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Workspace document size limits must be positive.");
        }

        Bytes = bytes;
    }

    public override string ToString()
    {
        return Bytes.ToString(CultureInfo.InvariantCulture);
    }
}
