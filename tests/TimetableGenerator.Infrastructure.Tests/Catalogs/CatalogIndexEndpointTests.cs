using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

[TestClass]
public sealed class CatalogIndexEndpointTests
{
    [TestMethod]
    public void ResolveCatalogUriUsesIndexDirectoryAndPreservesOrigin()
    {
        CatalogIndexEndpoint endpoint = new CatalogIndexEndpoint(new Uri("https://catalog.example.edu/catalog/v1/index.json"));
        CatalogRelativePath relativePath = new CatalogRelativePath(CatalogSynchronizationTestDocuments.VALID_RELATIVE_PATH);

        Uri catalogUri = endpoint.ResolveCatalogUri(relativePath);

        Assert.AreEqual("https://catalog.example.edu/catalog/v1/handong-global-university/2026-2/catalog-r0001.json", catalogUri.AbsoluteUri);
        Assert.IsTrue(endpoint.IsSameOrigin(catalogUri));
        Assert.IsFalse(endpoint.IsSameOrigin(new Uri("https://other.example.edu/catalog.json")));
    }

    [TestMethod]
    [DataRow("catalog/v1/index.json")]
    [DataRow("http://catalog.example.edu/catalog/v1/index.json")]
    [DataRow("ftp://catalog.example.edu/catalog/v1/index.json")]
    [DataRow("https://user:password@catalog.example.edu/catalog/v1/index.json")]
    [DataRow("https://catalog.example.edu/catalog/v1/index.json#current")]
    public void ConstructorRejectsUnsafeEndpoint(string endpointValue)
    {
        Uri endpointUri = new Uri(endpointValue, UriKind.RelativeOrAbsolute);

        Assert.ThrowsExactly<ArgumentException>(
            () => new CatalogIndexEndpoint(endpointUri));
    }
}
