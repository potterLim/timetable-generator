using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Storage;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Storage;

public sealed class BoundedLocalFileReaderTests
{
    [Fact]
    public void ExactLimitIsAcceptedFromAStreamWithoutLengthMetadata()
    {
        byte[] expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(expected))
        {
            byte[] actual = BoundedLocalFileReader.readAllBytes(source, expected.LongLength);

            Assert.Equal(expected, actual);
            Assert.Equal(expected.LongLength, source.Position);
        }
    }

    [Fact]
    public void OversizedStreamReadsOnlyOneBytePastTheLimit()
    {
        const long MAXIMUM_BYTE_COUNT = 8L;
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(new byte[64]))
        {
            BoundedLocalFileReadLimitException exception = Assert.Throws<BoundedLocalFileReadLimitException>(
                delegate
                {
                    BoundedLocalFileReader.readAllBytes(source, MAXIMUM_BYTE_COUNT);
                });

            Assert.Equal(MAXIMUM_BYTE_COUNT, exception.MaximumByteCount);
            Assert.Equal(MAXIMUM_BYTE_COUNT + 1L, source.Position);
        }
    }

    [Fact]
    public async Task OversizedStreamReadAsyncReadsOnlyOneBytePastTheLimitAsync()
    {
        const long MAXIMUM_BYTE_COUNT = 8L;
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(new byte[64]))
        {
            BoundedLocalFileReadLimitException exception = await Assert.ThrowsAsync<BoundedLocalFileReadLimitException>(
                async delegate
                {
                    await BoundedLocalFileReader.readAllBytesAsync(source, MAXIMUM_BYTE_COUNT, CancellationToken.None);
                });

            Assert.Equal(MAXIMUM_BYTE_COUNT, exception.MaximumByteCount);
            Assert.Equal(MAXIMUM_BYTE_COUNT + 1L, source.Position);
        }
    }

    [Fact]
    public async Task ReadAsyncObservesCancellationBeforeReadingAsync()
    {
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(new byte[64]))
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async delegate
                {
                    await BoundedLocalFileReader.readAllBytesAsync(source, 64L, cancellationSource.Token);
                });

            Assert.Equal(0L, source.Position);
        }
    }

    private sealed class LengthUnavailableMemoryStream : MemoryStream
    {
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public LengthUnavailableMemoryStream(byte[] content)
            : base(content, false)
        {
        }
    }
}
