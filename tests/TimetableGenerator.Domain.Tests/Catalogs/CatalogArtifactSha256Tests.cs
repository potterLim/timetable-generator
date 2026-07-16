using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Tests.Catalogs;

[TestClass]
public sealed class CatalogArtifactSha256Tests
{
    [TestMethod]
    public void Sha256PreservesValidatedLowercaseHexIdentity()
    {
        string hexValue = new string('a', 64);

        CatalogArtifactSha256 first = new CatalogArtifactSha256(hexValue);
        CatalogArtifactSha256 second = new CatalogArtifactSha256(hexValue);

        Assert.AreEqual(hexValue, first.HexValue);
        Assert.AreEqual(hexValue, first.ToString());
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Sha256RejectsMalformedHexValues()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new CatalogArtifactSha256(null!));
        Assert.ThrowsExactly<ArgumentException>(
            () => new CatalogArtifactSha256(new string('a', 63)));
        Assert.ThrowsExactly<ArgumentException>(
            () => new CatalogArtifactSha256(new string('A', 64)));
        Assert.ThrowsExactly<ArgumentException>(
            () => new CatalogArtifactSha256(new string('g', 64)));
    }
}
