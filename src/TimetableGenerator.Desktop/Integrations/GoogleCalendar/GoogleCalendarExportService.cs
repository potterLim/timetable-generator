using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarExportService : IGoogleCalendarExporter
{
    private readonly IGoogleAccessTokenProvider mAccessTokenProvider;
    private readonly GoogleCalendarApiClient mApiClient;
    private readonly IGoogleCalendarBindingStore mBindingStore;
    private readonly IGoogleCalendarExportLeaseProvider mExportLeaseProvider;
    private readonly SemaphoreSlim mExportGate;
    private readonly IDisposable? mOwnedResourcesOrNull;
    private readonly CancellationTokenSource mLifetimeCancellationSource;
    private readonly object mLifecycleLock;
    private int mActiveOperationCount;
    private bool mIsDisposed;
    private bool mLifetimeCancellationCompleted;
    private bool mResourcesWereReleased;

    public GoogleCalendarExportService(
        IGoogleAccessTokenProvider accessTokenProvider,
        GoogleCalendarApiClient apiClient,
        IGoogleCalendarBindingStore bindingStore)
        : this(
            accessTokenProvider,
            apiClient,
            bindingStore,
            NoOpGoogleCalendarExportLeaseProvider.Instance,
            null)
    {
    }

    internal GoogleCalendarExportService(
        IGoogleAccessTokenProvider accessTokenProvider,
        GoogleCalendarApiClient apiClient,
        IGoogleCalendarBindingStore bindingStore,
        IDisposable? ownedResourcesOrNull)
        : this(
            accessTokenProvider,
            apiClient,
            bindingStore,
            NoOpGoogleCalendarExportLeaseProvider.Instance,
            ownedResourcesOrNull)
    {
    }

    internal GoogleCalendarExportService(
        IGoogleAccessTokenProvider accessTokenProvider,
        GoogleCalendarApiClient apiClient,
        IGoogleCalendarBindingStore bindingStore,
        IGoogleCalendarExportLeaseProvider exportLeaseProvider,
        IDisposable? ownedResourcesOrNull)
    {
        if (accessTokenProvider == null)
        {
            throw new ArgumentNullException(nameof(accessTokenProvider));
        }

        if (apiClient == null)
        {
            throw new ArgumentNullException(nameof(apiClient));
        }

        if (bindingStore == null)
        {
            throw new ArgumentNullException(nameof(bindingStore));
        }

        if (exportLeaseProvider == null)
        {
            throw new ArgumentNullException(nameof(exportLeaseProvider));
        }

        mAccessTokenProvider = accessTokenProvider;
        mApiClient = apiClient;
        mBindingStore = bindingStore;
        mExportLeaseProvider = exportLeaseProvider;
        mOwnedResourcesOrNull = ownedResourcesOrNull;
        mExportGate = new SemaphoreSlim(1, 1);
        mLifetimeCancellationSource = new CancellationTokenSource();
        mLifecycleLock = new object();
    }

    public async Task<GoogleCalendarExportResult> ExportAsync(
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        CancellationTokenSource linkedCancellationSource = beginOperation(
            cancellationToken);
        bool gateWasAcquired = false;
        try
        {
            await mExportGate.WaitAsync(linkedCancellationSource.Token).ConfigureAwait(false);
            gateWasAcquired = true;
            GoogleOAuthAuthorizationResult authorizationResult =
                await mAccessTokenProvider.AuthorizeAsync(
                    linkedCancellationSource.Token).ConfigureAwait(false);
            if (authorizationResult.Status != EGoogleOAuthAuthorizationStatus.Completed)
            {
                return mapAuthorizationFailure(authorizationResult);
            }

            GoogleAccessToken? accessTokenOrNull = authorizationResult.AccessTokenOrNull;
            if (accessTokenOrNull == null)
            {
                return GoogleCalendarExportResult.Fail(
                    EGoogleCalendarExportStatus.AuthenticationFailed,
                    "access_token_missing");
            }

            GoogleAccessToken accessToken = accessTokenOrNull;
            await using (IGoogleCalendarExportLease exportLease =
                await mExportLeaseProvider.AcquireAsync(
                    linkedCancellationSource.Token).ConfigureAwait(false))
            {
                GoogleCalendarId calendarId = await findOrCreateCalendarAsync(
                    accessToken,
                    plan,
                    linkedCancellationSource.Token).ConfigureAwait(false);
                await mApiClient.UpdatePlanCalendarAsync(
                    accessToken,
                    calendarId,
                    plan,
                    linkedCancellationSource.Token).ConfigureAwait(false);
                GoogleCalendarReconciliationResult reconciliation =
                    await mApiClient.ReconcileEventsAsync(
                        accessToken,
                        calendarId,
                        plan,
                        linkedCancellationSource.Token).ConfigureAwait(false);
                return GoogleCalendarExportResult.Complete(
                    calendarId,
                    reconciliation);
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested == false
            && mLifetimeCancellationSource.IsCancellationRequested == false)
        {
            return GoogleCalendarExportResult.Fail(
                EGoogleCalendarExportStatus.NetworkFailed,
                "google_calendar_timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GoogleCalendarApiException exception)
        {
            return GoogleCalendarExportResult.Fail(
                exception.FailureKind == EGoogleCalendarApiFailureKind.Transient
                    ? EGoogleCalendarExportStatus.NetworkFailed
                    : mapApiFailure(exception.StatusCode),
                exception.DiagnosticCode);
        }
        catch (Exception exception) when (
            exception is HttpRequestException
            || exception is TimeoutException)
        {
            return GoogleCalendarExportResult.Fail(
                EGoogleCalendarExportStatus.NetworkFailed,
                "google_calendar_transport_failed");
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is JsonException
            || exception is InvalidOperationException)
        {
            return GoogleCalendarExportResult.Fail(
                EGoogleCalendarExportStatus.Failed,
                "google_calendar_local_state_failed");
        }
        finally
        {
            if (gateWasAcquired)
            {
                mExportGate.Release();
            }

            linkedCancellationSource.Dispose();
            endOperation();
        }
    }

    public void Dispose()
    {
        bool shouldCancel;
        lock (mLifecycleLock)
        {
            shouldCancel = mIsDisposed == false;
            mIsDisposed = true;
        }

        if (shouldCancel)
        {
            try
            {
                mLifetimeCancellationSource.Cancel();
            }
            finally
            {
                lock (mLifecycleLock)
                {
                    mLifetimeCancellationCompleted = true;
                }

                releaseResourcesWhenIdle();
            }
        }
    }

    private async Task<GoogleCalendarId> findOrCreateCalendarAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken)
    {
        GoogleCalendarId? boundCalendarIdOrNull =
            await mBindingStore.GetCalendarIdOrNullAsync(
                plan.PlanId,
                cancellationToken).ConfigureAwait(false);
        if (boundCalendarIdOrNull != null)
        {
            bool belongsToPlan = await mApiClient.IsCalendarOwnedByPlanAsync(
                accessToken,
                boundCalendarIdOrNull,
                plan.PlanId,
                cancellationToken).ConfigureAwait(false);
            if (belongsToPlan)
            {
                return boundCalendarIdOrNull;
            }

            await mBindingStore.DeleteCalendarIdAsync(
                plan.PlanId,
                cancellationToken).ConfigureAwait(false);
        }

        GoogleCalendarId? discoveredCalendarIdOrNull =
            await mApiClient.FindPlanCalendarOrNullAsync(
                accessToken,
                plan.PlanId,
                cancellationToken).ConfigureAwait(false);
        GoogleCalendarId calendarId;
        if (discoveredCalendarIdOrNull == null)
        {
            calendarId = await mApiClient.CreatePlanCalendarAsync(
                accessToken,
                plan,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            calendarId = discoveredCalendarIdOrNull;
        }

        await mBindingStore.SaveCalendarIdAsync(
            plan.PlanId,
            calendarId,
            cancellationToken).ConfigureAwait(false);
        return calendarId;
    }

    private static GoogleCalendarExportResult mapAuthorizationFailure(
        GoogleOAuthAuthorizationResult authorizationResult)
    {
        EGoogleCalendarExportStatus status = authorizationResult.Status switch
        {
            EGoogleOAuthAuthorizationStatus.NotConfigured =>
                EGoogleCalendarExportStatus.NotConfigured,
            EGoogleOAuthAuthorizationStatus.Cancelled =>
                EGoogleCalendarExportStatus.AuthenticationCancelled,
            EGoogleOAuthAuthorizationStatus.Failed =>
                EGoogleCalendarExportStatus.AuthenticationFailed,
            EGoogleOAuthAuthorizationStatus.NetworkFailed =>
                EGoogleCalendarExportStatus.NetworkFailed,
            EGoogleOAuthAuthorizationStatus.None =>
                throw new InvalidOperationException(
                    "Google Calendar authorization returned no status."),
            EGoogleOAuthAuthorizationStatus.Completed =>
                throw new InvalidOperationException(
                    "Completed authorization cannot be mapped as a failure."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(authorizationResult),
                authorizationResult.Status,
                "Unknown Google Calendar authorization status."),
        };
        return GoogleCalendarExportResult.Fail(
            status,
            authorizationResult.DiagnosticCodeOrNull);
    }

    private static EGoogleCalendarExportStatus mapApiFailure(HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            return EGoogleCalendarExportStatus.AuthenticationFailed;
        }

        if (statusCode == HttpStatusCode.Forbidden)
        {
            return EGoogleCalendarExportStatus.AccessDenied;
        }

        int numericStatusCode = (int)statusCode;
        if (statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || numericStatusCode >= 500)
        {
            return EGoogleCalendarExportStatus.NetworkFailed;
        }

        return EGoogleCalendarExportStatus.Failed;
    }

    private CancellationTokenSource beginOperation(CancellationToken cancellationToken)
    {
        lock (mLifecycleLock)
        {
            ObjectDisposedException.ThrowIf(mIsDisposed, this);
            mActiveOperationCount++;
            return CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                mLifetimeCancellationSource.Token);
        }
    }

    private void endOperation()
    {
        lock (mLifecycleLock)
        {
            mActiveOperationCount--;
        }

        releaseResourcesWhenIdle();
    }

    private void releaseResourcesWhenIdle()
    {
        IDisposable? resourcesToReleaseOrNull = null;
        bool shouldReleaseResources = false;
        lock (mLifecycleLock)
        {
            bool canRelease = mIsDisposed
                && mLifetimeCancellationCompleted
                && mActiveOperationCount == 0
                && mResourcesWereReleased == false;
            if (canRelease)
            {
                mResourcesWereReleased = true;
                shouldReleaseResources = true;
                resourcesToReleaseOrNull = mOwnedResourcesOrNull;
            }
        }

        if (shouldReleaseResources == false)
        {
            return;
        }

        try
        {
            resourcesToReleaseOrNull?.Dispose();
        }
        finally
        {
            mExportGate.Dispose();
            mLifetimeCancellationSource.Dispose();
        }
    }
}
