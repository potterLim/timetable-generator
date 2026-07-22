using System;
using System.IO;
using System.Threading;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed class GenerationFileStorageAccess : IDisposable
{
    private readonly FileStream mProcessLock;

    private readonly SemaphoreSlim mAccessGate;

    private bool mIsDisposed;

    public GenerationFileStorageAccess(FileStream processLock, SemaphoreSlim accessGate)
    {
        if (processLock == null)
        {
            throw new ArgumentNullException(nameof(processLock));
        }

        if (accessGate == null)
        {
            throw new ArgumentNullException(nameof(accessGate));
        }

        mProcessLock = processLock;
        mAccessGate = accessGate;
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        try
        {
            mProcessLock.Dispose();
        }
        finally
        {
            mAccessGate.Release();
        }
    }
}
