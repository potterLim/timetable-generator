using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TimetableGenerator.Infrastructure.Storage;

namespace TimetableGenerator.Infrastructure.Tests.Storage;

[TestClass]
public sealed class BoundedFileReaderTests
{
    [TestMethod]
    public async Task ExactLimitIsAcceptedFromAStreamWithoutLengthMetadataAsync()
    {
        byte[] expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(expected))
        {
            byte[] actual = await BoundedFileReader.readAllBytesAsync(source, expected.LongLength, CancellationToken.None);

            CollectionAssert.AreEqual(expected, actual);
            Assert.AreEqual(expected.LongLength, source.Position);
        }
    }

    [TestMethod]
    public async Task OversizedStreamReadsOnlyOneBytePastTheLimitAsync()
    {
        const long MAXIMUM_BYTE_COUNT = 8L;
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(new byte[64]))
        {
            BoundedFileReadLimitException exception = await Assert.ThrowsExactlyAsync<BoundedFileReadLimitException>(() => BoundedFileReader.readAllBytesAsync(source, MAXIMUM_BYTE_COUNT, CancellationToken.None));

            Assert.AreEqual(MAXIMUM_BYTE_COUNT, exception.MaximumByteCount);
            Assert.AreEqual(MAXIMUM_BYTE_COUNT + 1L, source.Position);
        }
    }

    [TestMethod]
    public async Task ReadObservesCancellationBeforeReadingAsync()
    {
        using (LengthUnavailableMemoryStream source = new LengthUnavailableMemoryStream(new byte[64]))
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            cancellationSource.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => BoundedFileReader.readAllBytesAsync(source, 64L, cancellationSource.Token));

            Assert.AreEqual(0L, source.Position);
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
