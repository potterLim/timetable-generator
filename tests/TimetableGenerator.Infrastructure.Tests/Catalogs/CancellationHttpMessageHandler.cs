using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

internal sealed class CancellationHttpMessageHandler : HttpMessageHandler
{
    private readonly TaskCompletionSource mRequestStartedSource;

    public Task RequestStarted
    {
        get
        {
            return mRequestStartedSource.Task;
        }
    }

    public CancellationHttpMessageHandler()
    {
        mRequestStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        mRequestStartedSource.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
