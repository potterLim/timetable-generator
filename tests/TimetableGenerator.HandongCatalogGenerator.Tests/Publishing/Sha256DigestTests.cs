using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Publishing;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Publishing;

[TestClass]
public sealed class Sha256DigestTests
{
    [TestMethod]
    public void Compute_KnownContent_ReturnsExpectedLowercaseHex()
    {
        byte[] content = Encoding.UTF8.GetBytes("abc");

        Sha256Digest digest = Sha256Digest.Compute(content);

        Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", digest.HexValue);
    }
}
