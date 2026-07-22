using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Product;

namespace TimetableGenerator.Desktop.Tests.Product;

internal sealed class QueueProductWorkspaceLoader : IProductWorkspaceLoader
{
    private readonly Queue<Func<CancellationToken, Task<ProductWorkspacePresentation>>> mLoads;

    public int LoadCount { get; private set; }

    public QueueProductWorkspaceLoader(
        IEnumerable<Func<CancellationToken, Task<ProductWorkspacePresentation>>> loads)
    {
        if (loads == null)
        {
            throw new ArgumentNullException(nameof(loads));
        }

        mLoads = new Queue<Func<CancellationToken, Task<ProductWorkspacePresentation>>>(loads);
    }

    public Task<ProductWorkspacePresentation> LoadAsync(CancellationToken cancellationToken)
    {
        ++LoadCount;
        if (mLoads.Count == 0)
        {
            throw new InvalidOperationException("No queued workspace load remains.");
        }

        Func<CancellationToken, Task<ProductWorkspacePresentation>> load = mLoads.Dequeue();
        return load(cancellationToken);
    }
}
