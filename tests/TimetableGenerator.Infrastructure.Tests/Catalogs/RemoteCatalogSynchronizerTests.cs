using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

[TestClass]
public sealed class RemoteCatalogSynchronizerTests
{
    private static readonly Uri INDEX_URI = new Uri("https://catalog.example.edu/catalog/v1/index.json");

    [TestMethod]
    public void PublicSurfaceDoesNotAcceptAnOpaqueRedirectingHttpClient()
    {
        ConstructorInfo[] publicConstructors = typeof(RemoteCatalogSynchronizer).GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.IsEmpty(publicConstructors);
    }

    [TestMethod]
    public async Task SynchronizeDownloadsVerifiesAndInstallsDefaultCatalogAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(catalogBytes);
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[]
                {
                    createResponse(indexBytes),
                    createResponse(catalogBytes),
                }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, createLimits());
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, createLimits());

                VerifiedCatalogPackage package = await synchronizer.SynchronizeDefaultCatalogAsync(CancellationToken.None);
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                Assert.AreEqual("handong-global-university:2026-2:r0001", package.Document.Catalog.Id.Value);
                Assert.AreEqual(2, handler.RequestCount);
                Assert.AreEqual(INDEX_URI, handler.RequestedUris[0]);
                Assert.AreEqual("https://catalog.example.edu/catalog/v1/handong-global-university/2026-2/catalog-r0001.json", handler.RequestedUris[1].AbsoluteUri);
                Assert.AreEqual(ECatalogCacheLoadStatus.LoadedLatestGeneration, cachedResult.Status);
                Assert.AreEqual(package.Document.Catalog.Id, cachedResult.GetPackage().Document.Catalog.Id);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task DownloadVerifiesWithoutInstallingDefaultCatalogAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(catalogBytes);
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[]
                {
                    createResponse(indexBytes),
                    createResponse(catalogBytes),
                }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogSynchronizationLimits limits = createLimits();
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                VerifiedCatalogPackage package = await synchronizer.DownloadDefaultCatalogAsync(CancellationToken.None);
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                Assert.AreEqual("handong-global-university:2026-2:r0001", package.Document.Catalog.Id.Value);
                Assert.AreEqual(2, handler.RequestCount);
                Assert.AreEqual(ECatalogCacheLoadStatus.NotFound, cachedResult.Status);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeRejectsOversizedIndexBeforeParsingAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(catalogBytes);
            CatalogSynchronizationLimits limits = new CatalogSynchronizationLimits(new CatalogResourceByteLimit(indexBytes.LongLength - 1L), new CatalogResourceByteLimit(1_000_000L));
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[] { createResponse(indexBytes) }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                Assert.AreEqual(1, handler.RequestCount);
                Assert.IsFalse(cachedResult.IsFound);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeRejectsDeclaredCatalogAboveLimitBeforeRequestAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(catalogBytes);
            CatalogSynchronizationLimits limits = new CatalogSynchronizationLimits(new CatalogResourceByteLimit(64_000L), new CatalogResourceByteLimit(catalogBytes.LongLength - 1L));
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[] { createResponse(indexBytes) }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));

                Assert.AreEqual(1, handler.RequestCount);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeCapsUnknownLengthCatalogResponseAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            const int CATALOG_LIMIT_BYTES = 100;
            byte[] oversizedCatalogBytes = new byte[CATALOG_LIMIT_BYTES + 1];
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateIndexBytes(CatalogSynchronizationTestDocuments.VALID_RELATIVE_PATH, new CatalogFileSize(1L), Sha256Digest.Compute(new byte[] { 0x01 }));
            CatalogSynchronizationLimits limits = new CatalogSynchronizationLimits(new CatalogResourceByteLimit(64_000L), new CatalogResourceByteLimit(CATALOG_LIMIT_BYTES));
            HttpResponseMessage catalogResponse = new HttpResponseMessage(HttpStatusCode.OK);
            catalogResponse.Content = new UnknownLengthByteArrayContent(oversizedCatalogBytes);
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[]
                {
                    createResponse(indexBytes),
                    catalogResponse,
                }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                Assert.AreEqual(2, handler.RequestCount);
                Assert.IsFalse(cachedResult.IsFound);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeVerifiesShaBeforeChangingExistingCacheAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogSynchronizationLimits limits = createLimits();
            CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
            await store.SaveAsync(CatalogSynchronizationTestDocuments.CreateVerifiedPackageWithKoreanName("보존할 자료구조"), CancellationToken.None);
            byte[][] contentBeforeSynchronization = await readGenerationContentsAsync(testDirectoryPath);
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] changedCatalogBytes = CatalogSynchronizationTestDocuments.Replace(catalogBytes, "Data Structures", "Changed Data Structures");
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(catalogBytes);
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[]
                {
                    createResponse(indexBytes),
                    createResponse(changedCatalogBytes),
                }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));
                byte[][] contentAfterSynchronization = await readGenerationContentsAsync(testDirectoryPath);
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                assertGenerationContentsEqual(contentBeforeSynchronization, contentAfterSynchronization);
                Assert.AreEqual("보존할 자료구조", cachedResult.GetPackage().Document.Catalog.Courses[0].KoreanName.Value);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeParsesCatalogBeforeFirstInstallAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] invalidCatalogBytes = CatalogSynchronizationTestDocuments.Replace(CatalogSynchronizationTestDocuments.CreateValidCatalogBytes(), "\"courses\": 1,", "\"courses\": 2,");
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(invalidCatalogBytes);
            CatalogSynchronizationLimits limits = createLimits();
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[]
                {
                    createResponse(indexBytes),
                    createResponse(invalidCatalogBytes),
                }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                Assert.IsFalse(cachedResult.IsFound);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeRejectsAbsoluteCatalogPathBeforeSecondRequestAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateIndexBytes("https://other.example.edu/catalog.json", new CatalogFileSize(catalogBytes.LongLength), Sha256Digest.Compute(catalogBytes));
            CatalogSynchronizationLimits limits = createLimits();
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[] { createResponse(indexBytes) }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));

                Assert.AreEqual(1, handler.RequestCount);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizeRejectsCrossOriginCatalogRedirectAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            byte[] catalogBytes = CatalogSynchronizationTestDocuments.CreateValidCatalogBytes();
            byte[] indexBytes = CatalogSynchronizationTestDocuments.CreateValidIndexBytes(catalogBytes);
            HttpResponseMessage redirectedCatalogResponse = createResponse(catalogBytes);
            redirectedCatalogResponse.RequestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri("https://other.example.edu/catalog.json"));
            CatalogSynchronizationLimits limits = createLimits();
            using (QueueHttpMessageHandler handler = new QueueHttpMessageHandler(
                new HttpResponseMessage[]
                {
                    createResponse(indexBytes),
                    redirectedCatalogResponse,
                }))
            using (HttpClient httpClient = new HttpClient(handler))
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);

                await Assert.ThrowsExactlyAsync<RemoteCatalogSynchronizationException>(
                    () => synchronizer.SynchronizeDefaultCatalogAsync(
                        CancellationToken.None));
                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);

                Assert.AreEqual(2, handler.RequestCount);
                Assert.IsFalse(cachedResult.IsFound);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task SynchronizePropagatesCancellationWithoutInstallingAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            CatalogSynchronizationLimits limits = createLimits();
            using (CancellationHttpMessageHandler handler = new CancellationHttpMessageHandler())
            using (HttpClient httpClient = new HttpClient(handler))
            using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
            {
                CatalogCacheFileStore store = createStore(testDirectoryPath, limits);
                RemoteCatalogSynchronizer synchronizer = createSynchronizer(httpClient, store, limits);
                Task<VerifiedCatalogPackage> synchronizationTask = synchronizer.SynchronizeDefaultCatalogAsync(cancellationSource.Token);
                await handler.RequestStarted;

                cancellationSource.Cancel();
                bool wasCanceled = false;
                try
                {
                    await synchronizationTask;
                }
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                }

                CatalogCacheLoadResult cachedResult = await store.LoadAsync(CancellationToken.None);
                Assert.IsTrue(wasCanceled);
                Assert.IsFalse(cachedResult.IsFound);
            }
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    private static RemoteCatalogSynchronizer createSynchronizer(HttpClient httpClient, CatalogCacheFileStore store, CatalogSynchronizationLimits limits)
    {
        return RemoteCatalogSynchronizer.createForTesting(httpClient, new CatalogIndexEndpoint(INDEX_URI), limits, store);
    }

    private static CatalogCacheFileStore createStore(string testDirectoryPath, CatalogSynchronizationLimits limits)
    {
        CatalogCacheFilePath cachePath = new CatalogCacheFilePath(Path.Combine(testDirectoryPath, "catalog.cache"));
        return new CatalogCacheFileStore(cachePath, limits);
    }

    private static CatalogSynchronizationLimits createLimits()
    {
        return new CatalogSynchronizationLimits(new CatalogResourceByteLimit(64_000L), new CatalogResourceByteLimit(1_000_000L));
    }

    private static HttpResponseMessage createResponse(byte[] content)
    {
        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new ByteArrayContent(content);
        return response;
    }

    private static string createTestDirectoryPath()
    {
        return Path.Combine(Path.GetTempPath(), "TimetableGenerator.Tests", Guid.NewGuid().ToString("N"));
    }

    private static async Task<byte[][]> readGenerationContentsAsync(string testDirectoryPath)
    {
        string[] generationPaths = Directory.GetFiles(testDirectoryPath, "catalog.g*.cache");
        Array.Sort(generationPaths, StringComparer.Ordinal);
        byte[][] contents = new byte[generationPaths.Length][];
        for (int index = 0; index < generationPaths.Length; ++index)
        {
            contents[index] = await File.ReadAllBytesAsync(generationPaths[index], CancellationToken.None);
        }

        return contents;
    }

    private static void assertGenerationContentsEqual(byte[][] expectedContents, byte[][] actualContents)
    {
        Assert.HasCount(expectedContents.Length, actualContents);
        for (int index = 0; index < expectedContents.Length; ++index)
        {
            CollectionAssert.AreEqual(expectedContents[index], actualContents[index]);
        }
    }

    private static void deleteTestDirectory(string testDirectoryPath)
    {
        if (Directory.Exists(testDirectoryPath))
        {
            Directory.Delete(testDirectoryPath, true);
        }
    }
}
