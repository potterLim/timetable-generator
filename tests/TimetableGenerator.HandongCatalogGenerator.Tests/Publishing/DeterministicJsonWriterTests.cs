using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Publishing;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Publishing;

[TestClass]
public sealed class DeterministicJsonWriterTests
{
    [TestMethod]
    public void Write_KoreanText_UsesUtf8WithoutBomAndLfLineEndings()
    {
        byte[] content = DeterministicJsonWriter.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("name", "한동대학교");
                writer.WriteEndObject();
            });

        byte[] utf8Bom = Encoding.UTF8.Preamble.ToArray();
        Assert.IsFalse(content.AsSpan().StartsWith(utf8Bom));
        Assert.AreEqual((byte)'\n', content[^1]);
        Assert.IsFalse(content.AsSpan().Contains((byte)'\r'));
        Assert.IsTrue(Encoding.UTF8.GetString(content).Contains("한동대학교", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Write_SameDocumentTwice_ProducesIdenticalBytesAndHash()
    {
        static byte[] writeDocument()
        {
            return DeterministicJsonWriter.Write(
                writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("value", 42);
                    writer.WriteEndObject();
                });
        }

        byte[] first = writeDocument();
        byte[] second = writeDocument();

        CollectionAssert.AreEqual(first, second);
        Assert.AreEqual(Sha256Digest.Compute(first), Sha256Digest.Compute(second));
    }
}
