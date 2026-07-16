using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;

namespace TimetableGenerator.Desktop.Tests.Product;

internal sealed class QueueProductWorkspaceLoader : IProductWorkspaceLoader
{
    private readonly Queue<Func<CancellationToken, Task<PlannerWorkspaceViewModel>>>
        mLoads;

    public int LoadCount { get; private set; }

    public QueueProductWorkspaceLoader(
        IEnumerable<Func<CancellationToken, Task<PlannerWorkspaceViewModel>>> loads)
    {
        if (loads == null)
        {
            throw new ArgumentNullException(nameof(loads));
        }

        mLoads = new Queue<Func<CancellationToken, Task<PlannerWorkspaceViewModel>>>(
            loads);
    }

    public Task<PlannerWorkspaceViewModel> LoadAsync(
        CancellationToken cancellationToken)
    {
        ++LoadCount;
        if (mLoads.Count == 0)
        {
            throw new InvalidOperationException("No queued workspace load remains.");
        }

        Func<CancellationToken, Task<PlannerWorkspaceViewModel>> load =
            mLoads.Dequeue();
        return load(cancellationToken);
    }
}
