using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Product.Loading;

namespace TimetableGenerator.Desktop.Tests.Product.Loading;

internal sealed class FixedProductWorkspaceDataLoader : IProductWorkspaceDataLoader
{
    private readonly ProductWorkspaceLoadResult mLoadResult;

    public FixedProductWorkspaceDataLoader(ProductWorkspaceLoadResult loadResult)
    {
        if (loadResult == null)
        {
            throw new ArgumentNullException(nameof(loadResult));
        }

        mLoadResult = loadResult;
    }

    public Task<ProductWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(mLoadResult);
    }
}
