using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.CatalogJson;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class RemoteCatalogSynchronizer : IDisposable
{
    private const int DOWNLOAD_BUFFER_SIZE = 16_384;

    private static readonly TimeSpan CONNECTION_TIMEOUT = TimeSpan.FromSeconds(10.0);

    private static readonly TimeSpan HTTP_TIMEOUT = TimeSpan.FromSeconds(30.0);

    private static readonly TimeSpan POOLED_CONNECTION_LIFETIME = TimeSpan.FromMinutes(15.0);

    private readonly HttpClient mHttpClient;

    private readonly CatalogIndexEndpoint mEndpoint;

    private readonly CatalogSynchronizationLimits mLimits;

    private readonly CatalogCacheFileStore mCacheStore;

    private readonly EHttpClientOwnership mHttpClientOwnership;

    private RemoteCatalogSynchronizer(
        HttpClient httpClient,
        CatalogIndexEndpoint endpoint,
        CatalogSynchronizationLimits limits,
        CatalogCacheFileStore cacheStore,
        EHttpClientOwnership httpClientOwnership)
    {
        if (httpClient == null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }

        if (endpoint == null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        if (cacheStore == null)
        {
            throw new ArgumentNullException(nameof(cacheStore));
        }

        mHttpClient = httpClient;
        mEndpoint = endpoint;
        mLimits = limits;
        mCacheStore = cacheStore;
        mHttpClientOwnership = httpClientOwnership;
    }

    public static RemoteCatalogSynchronizer Create(
        CatalogIndexEndpoint endpoint,
        CatalogSynchronizationLimits limits,
        CatalogCacheFileStore cacheStore)
    {
        if (endpoint == null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        if (cacheStore == null)
        {
            throw new ArgumentNullException(nameof(cacheStore));
        }

        SocketsHttpHandler handler = new SocketsHttpHandler();
        handler.AllowAutoRedirect = false;
        handler.AutomaticDecompression = DecompressionMethods.None;
        handler.ConnectTimeout = CONNECTION_TIMEOUT;
        handler.PooledConnectionLifetime = POOLED_CONNECTION_LIFETIME;
        handler.UseCookies = false;

        HttpClient httpClient = new HttpClient(handler, true);
        httpClient.Timeout = HTTP_TIMEOUT;
        return new RemoteCatalogSynchronizer(
            httpClient,
            endpoint,
            limits,
            cacheStore,
            EHttpClientOwnership.Synchronizer);
    }

    public void Dispose()
    {
        if (mHttpClientOwnership == EHttpClientOwnership.Synchronizer)
        {
            mHttpClient.Dispose();
        }
    }

    public async Task<VerifiedCatalogPackage> SynchronizeDefaultCatalogAsync(
        CancellationToken cancellationToken)
    {
        VerifiedCatalogPackage package = await DownloadDefaultCatalogAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await mCacheStore.SaveAsync(package, cancellationToken).ConfigureAwait(false);
            return package;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CatalogCacheUpgradeRequiredException)
        {
            throw;
        }
        catch (CatalogCachePersistenceException exception)
        {
            throw new RemoteCatalogSynchronizationException(
                ERemoteCatalogSynchronizationFailureKind.LocalPersistence,
                "The verified remote catalog could not be installed in the offline cache.",
                exception);
        }
    }

    public async Task<VerifiedCatalogPackage> DownloadDefaultCatalogAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            byte[] indexBytes = await downloadContentAsync(
                mEndpoint.Value,
                mLimits.Index,
                cancellationToken).ConfigureAwait(false);
            CatalogIndexDocument index = CatalogIndexJsonReader.Read(indexBytes);
            CatalogIndexEntry entry = index.FindDefaultEntry();
            if (entry.File.Size.Value > mLimits.Catalog.Bytes)
            {
                throw new RemoteCatalogSynchronizationException(
                    ERemoteCatalogSynchronizationFailureKind.ResourceLimit,
                    "The index declares a catalog larger than the configured download limit.");
            }

            Uri catalogUri = mEndpoint.ResolveCatalogUri(entry.File.RelativePath);
            CatalogResourceByteLimit declaredCatalogLimit = new CatalogResourceByteLimit(entry.File.Size.Value);
            byte[] catalogBytes = await downloadContentAsync(
                catalogUri,
                declaredCatalogLimit,
                cancellationToken).ConfigureAwait(false);
            VerifiedCatalogPackage package = VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
            return package;
        }
        catch (OperationCanceledException exception) when (
            cancellationToken.IsCancellationRequested == false)
        {
            throw new RemoteCatalogSynchronizationException(
                ERemoteCatalogSynchronizationFailureKind.Network,
                "The remote catalog request timed out.",
                exception);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RemoteCatalogSynchronizationException)
        {
            throw;
        }
        catch (CatalogJsonFormatException exception)
        {
            throw new RemoteCatalogSynchronizationException(
                ERemoteCatalogSynchronizationFailureKind.InvalidRemoteData,
                "The remote catalog package failed strict verification.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new RemoteCatalogSynchronizationException(
                ERemoteCatalogSynchronizationFailureKind.Network,
                "The remote catalog service could not be reached successfully.",
                exception);
        }
        catch (IOException exception)
        {
            throw new RemoteCatalogSynchronizationException(
                ERemoteCatalogSynchronizationFailureKind.Network,
                "The remote catalog response could not be read.",
                exception);
        }
    }

    internal static RemoteCatalogSynchronizer createForTesting(
        HttpClient httpClient,
        CatalogIndexEndpoint endpoint,
        CatalogSynchronizationLimits limits,
        CatalogCacheFileStore cacheStore)
    {
        return new RemoteCatalogSynchronizer(
            httpClient,
            endpoint,
            limits,
            cacheStore,
            EHttpClientOwnership.External);
    }

    private async Task<byte[]> downloadContentAsync(
        Uri resourceUri,
        CatalogResourceByteLimit byteLimit,
        CancellationToken cancellationToken)
    {
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, resourceUri))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
            using (HttpResponseMessage response = await mHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                requireSameOriginResponse(response);
                if (response.IsSuccessStatusCode == false)
                {
                    throw new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.Network,
                        "The remote catalog service returned HTTP status "
                        + (int)response.StatusCode
                        + ".");
                }

                long? declaredLengthOrNull = response.Content.Headers.ContentLength;
                if (declaredLengthOrNull.HasValue && declaredLengthOrNull.Value > byteLimit.Bytes)
                {
                    throw new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.ResourceLimit,
                        "The remote catalog response exceeds its configured size limit.");
                }

                return await readBoundedContentAsync(
                    response.Content,
                    declaredLengthOrNull,
                    byteLimit,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void requireSameOriginResponse(HttpResponseMessage response)
    {
        Uri? responseUriOrNull = response.RequestMessage?.RequestUri;
        if (responseUriOrNull == null || mEndpoint.IsSameOrigin(responseUriOrNull) == false)
        {
            throw new RemoteCatalogSynchronizationException(
                ERemoteCatalogSynchronizationFailureKind.SecurityPolicy,
                "The remote catalog service redirected outside the configured origin.");
        }
    }

    private static async Task<byte[]> readBoundedContentAsync(
        HttpContent content,
        long? declaredLengthOrNull,
        CatalogResourceByteLimit byteLimit,
        CancellationToken cancellationToken)
    {
        int initialCapacity = 0;
        if (declaredLengthOrNull.HasValue)
        {
            initialCapacity = checked((int)declaredLengthOrNull.Value);
        }

        using (Stream responseStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        using (MemoryStream contentStream = new MemoryStream(initialCapacity))
        {
            byte[] buffer = new byte[DOWNLOAD_BUFFER_SIZE];
            while (true)
            {
                int readCount = await responseStream.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken).ConfigureAwait(false);
                if (readCount == 0)
                {
                    break;
                }

                long nextLength = contentStream.Length + readCount;
                if (nextLength > byteLimit.Bytes)
                {
                    throw new RemoteCatalogSynchronizationException(
                        ERemoteCatalogSynchronizationFailureKind.ResourceLimit,
                        "The remote catalog response exceeds its configured size limit.");
                }

                contentStream.Write(buffer, 0, readCount);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return contentStream.ToArray();
        }
    }
}
