using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

internal sealed class UnknownLengthByteArrayContent : HttpContent
{
    private readonly byte[] mContent;

    public UnknownLengthByteArrayContent(byte[] content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        mContent = content;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return stream.WriteAsync(mContent, 0, mContent.Length);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        return stream.WriteAsync(mContent, cancellationToken).AsTask();
    }

    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        Stream stream = new MemoryStream(mContent, false);
        return Task.FromResult(stream);
    }

    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new MemoryStream(mContent, false);
        return Task.FromResult(stream);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0L;
        return false;
    }
}
