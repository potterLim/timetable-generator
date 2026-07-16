using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> mResponses;

    private readonly List<Uri> mRequestedUris;

    public int RequestCount
    {
        get
        {
            return mRequestedUris.Count;
        }
    }

    public IReadOnlyList<Uri> RequestedUris
    {
        get
        {
            return mRequestedUris.AsReadOnly();
        }
    }

    public QueueHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
    {
        if (responses == null)
        {
            throw new ArgumentNullException(nameof(responses));
        }

        mResponses = new Queue<HttpResponseMessage>(responses);
        mRequestedUris = new List<Uri>();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri == null)
        {
            throw new InvalidOperationException("Catalog test requests require an absolute URI.");
        }

        if (mResponses.Count == 0)
        {
            throw new InvalidOperationException("No catalog test response remains in the queue.");
        }

        mRequestedUris.Add(request.RequestUri);
        HttpResponseMessage response = mResponses.Dequeue();
        if (response.RequestMessage == null)
        {
            response.RequestMessage = request;
        }

        return Task.FromResult(response);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            foreach (HttpResponseMessage response in mResponses)
            {
                response.Dispose();
            }

            mResponses.Clear();
        }

        base.Dispose(isDisposing);
    }
}
