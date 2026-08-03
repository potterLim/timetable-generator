using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarExportService : IGoogleCalendarExporter
{
    private const int MAXIMUM_DESTINATION_SELECTION_ATTEMPTS = 3;

    private readonly IGoogleAccessTokenProvider mAccessTokenProvider;
    private readonly GoogleCalendarApiClient mApiClient;
    private readonly IGoogleCalendarExportLeaseProvider mExportLeaseProvider;
    private readonly SemaphoreSlim mExportGate;
    private readonly IDisposable? mOwnedResourcesOrNull;
    private readonly CancellationTokenSource mLifetimeCancellationSource;
    private readonly object mLifecycleLock;
    private int mActiveOperationCount;
    private bool mIsDisposed;
    private bool mLifetimeCancellationCompleted;
    private bool mResourcesWereReleased;

    public GoogleCalendarExportService(IGoogleAccessTokenProvider accessTokenProvider, GoogleCalendarApiClient apiClient)
        : this(accessTokenProvider, apiClient, NoOpGoogleCalendarExportLeaseProvider.Instance, null)
    {
    }

    internal GoogleCalendarExportService(IGoogleAccessTokenProvider accessTokenProvider, GoogleCalendarApiClient apiClient, IDisposable? ownedResourcesOrNull)
        : this(accessTokenProvider, apiClient, NoOpGoogleCalendarExportLeaseProvider.Instance, ownedResourcesOrNull)
    {
    }

    internal GoogleCalendarExportService(IGoogleAccessTokenProvider accessTokenProvider, GoogleCalendarApiClient apiClient, IGoogleCalendarExportLeaseProvider exportLeaseProvider, IDisposable? ownedResourcesOrNull)
    {
        if (accessTokenProvider == null)
        {
            throw new ArgumentNullException(nameof(accessTokenProvider));
        }

        if (apiClient == null)
        {
            throw new ArgumentNullException(nameof(apiClient));
        }

        if (exportLeaseProvider == null)
        {
            throw new ArgumentNullException(nameof(exportLeaseProvider));
        }

        mAccessTokenProvider = accessTokenProvider;
        mApiClient = apiClient;
        mExportLeaseProvider = exportLeaseProvider;
        mOwnedResourcesOrNull = ownedResourcesOrNull;
        mExportGate = new SemaphoreSlim(1, 1);
        mLifetimeCancellationSource = new CancellationTokenSource();
        mLifecycleLock = new object();
    }

    public async Task<GoogleCalendarExportResult> ExportAsync(GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (conflictResolver == null)
        {
            throw new ArgumentNullException(nameof(conflictResolver));
        }

        CancellationTokenSource linkedCancellationSource = beginOperation(cancellationToken);
        bool gateWasAcquired = false;
        try
        {
            await mExportGate.WaitAsync(linkedCancellationSource.Token).ConfigureAwait(false);
            gateWasAcquired = true;
            await using (IGoogleCalendarExportLease exportLease = await mExportLeaseProvider.AcquireAsync(linkedCancellationSource.Token).ConfigureAwait(false))
            {
                GoogleOAuthAuthorizationResult authorizationResult = await mAccessTokenProvider.AuthorizeAsync(linkedCancellationSource.Token).ConfigureAwait(false);
                if (authorizationResult.Status != EGoogleOAuthAuthorizationStatus.Completed)
                {
                    return mapAuthorizationFailure(authorizationResult);
                }

                GoogleAccessToken? accessTokenOrNull = authorizationResult.AccessTokenOrNull;
                if (accessTokenOrNull == null)
                {
                    return GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.AuthenticationFailed, "access_token_missing");
                }

                GoogleAccessToken accessToken = accessTokenOrNull;
                GoogleCalendarDestination? destinationOrNull = await selectDestinationAsync(accessToken, plan, conflictResolver, linkedCancellationSource.Token).ConfigureAwait(false);
                if (destinationOrNull == null)
                {
                    return GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.Cancelled, "calendar_name_conflict_cancelled");
                }

                GoogleCalendarDestination destination = destinationOrNull;
                GoogleCalendarId calendarId;
                if (destination.ExistingCalendarIdOrNull == null)
                {
                    calendarId = await mApiClient.CreatePlanCalendarAsync(accessToken, destination.Plan, linkedCancellationSource.Token).ConfigureAwait(false);
                }
                else
                {
                    calendarId = destination.ExistingCalendarIdOrNull;
                    await mApiClient.UpdatePlanCalendarAsync(accessToken, calendarId, destination.Plan, destination.PendingManagedPlanId, linkedCancellationSource.Token).ConfigureAwait(false);
                }
                GoogleCalendarReconciliationResult reconciliation = await mApiClient.ReconcileEventsAsync(accessToken, calendarId, destination.Plan, destination.ReplacedPlanIdOrNull, linkedCancellationSource.Token).ConfigureAwait(false);
                await mApiClient.FinalizePlanCalendarAsync(accessToken, calendarId, destination.Plan, linkedCancellationSource.Token).ConfigureAwait(false);
                return GoogleCalendarExportResult.Complete(calendarId, destination.Plan.CalendarName, reconciliation);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false && mLifetimeCancellationSource.IsCancellationRequested == false)
        {
            return GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.NetworkFailed, "google_calendar_timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GoogleCalendarApiException exception)
        {
            Trace.TraceError("Google Calendar export failed with diagnostic code '{0}': {1}", exception.DiagnosticCode, exception);
            EGoogleCalendarExportStatus status;
            if (exception.FailureKind == EGoogleCalendarApiFailureKind.Transient)
            {
                status = EGoogleCalendarExportStatus.NetworkFailed;
            }
            else
            {
                status = mapApiFailure(exception.StatusCode);
            }

            return GoogleCalendarExportResult.Fail(status, exception.DiagnosticCode);
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is TimeoutException)
        {
            Trace.TraceError("Google Calendar export transport failed: {0}", exception);
            return GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.NetworkFailed, "google_calendar_transport_failed");
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is JsonException
            || exception is InvalidOperationException)
        {
            Trace.TraceError("Google Calendar export infrastructure failed: {0}", exception);
            return GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.Failed, "google_calendar_local_state_failed");
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

    private async Task<GoogleCalendarDestination?> selectDestinationAsync(GoogleAccessToken accessToken, GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleCalendarDescriptor> calendars = await mApiClient.ListCalendarsAsync(accessToken, cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; attempt < MAXIMUM_DESTINATION_SELECTION_ATTEMPTS; ++attempt)
        {
            IReadOnlyList<GoogleCalendarDescriptor> matches = await findNameMatchesWithResolvedOwnershipAsync(accessToken, plan.CalendarName, calendars, cancellationToken).ConfigureAwait(false);
            if (matches.Count == 0)
            {
                if (attempt > 0)
                {
                    throw new InvalidOperationException("The original Google calendar name conflict changed before confirmation.");
                }

                return GoogleCalendarDestination.CreateNew(plan);
            }

            GoogleCalendarDescriptor? replaceableCalendarOrNull = findSoleReplaceableCalendarOrNull(matches);
            ECalendarReplacementAvailability replacementAvailability = ECalendarReplacementAvailability.Unavailable;
            if (replaceableCalendarOrNull != null)
            {
                replacementAvailability = ECalendarReplacementAvailability.Available;
            }

            PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(plan.CalendarName, getExistingNames(calendars));
            CalendarNameConflict conflict = new CalendarNameConflict(ECalendarExportProvider.Google, plan.CalendarName, nextAvailableName, replacementAvailability);
            ECalendarNameConflictResolution resolution = await conflictResolver.ResolveAsync(conflict, cancellationToken).ConfigureAwait(false);
            CalendarNameConflictPolicy.EnsureResolutionIsSupported(conflict, resolution);

            if (resolution == ECalendarNameConflictResolution.Cancel)
            {
                return null;
            }

            IReadOnlyList<GoogleCalendarDescriptor> currentCalendars = await mApiClient.ListCalendarsAsync(accessToken, cancellationToken).ConfigureAwait(false);
            if (resolution == ECalendarNameConflictResolution.CreateWithAvailableName)
            {
                if (CalendarNameConflictPolicy.IsNameInUse(nextAvailableName, getExistingNames(currentCalendars)) == false)
                {
                    return GoogleCalendarDestination.CreateNew(plan.WithCalendarName(nextAvailableName));
                }

                calendars = currentCalendars;
                continue;
            }

            IReadOnlyList<GoogleCalendarDescriptor> currentMatches = await findNameMatchesWithResolvedOwnershipAsync(accessToken, plan.CalendarName, currentCalendars, cancellationToken).ConfigureAwait(false);
            GoogleCalendarDescriptor? currentCalendarOrNull = findCalendarByIdOrNull(replaceableCalendarOrNull!.CalendarId, currentMatches);
            if (currentCalendarOrNull == null || isSafeReplacementTarget(currentCalendarOrNull, replaceableCalendarOrNull, plan.CalendarName) == false)
            {
                throw new InvalidOperationException("The selected Google calendar is no longer safe to replace.");
            }

            return GoogleCalendarDestination.Replace(plan, replaceableCalendarOrNull.CalendarId, replaceableCalendarOrNull.ManagedPlanIdOrNull!.Value);
        }

        throw new InvalidOperationException("The Google calendar destination changed too many times.");
    }

    private async Task<IReadOnlyList<GoogleCalendarDescriptor>> findNameMatchesWithResolvedOwnershipAsync(GoogleAccessToken accessToken, PlanName requestedName, IReadOnlyList<GoogleCalendarDescriptor> calendars, CancellationToken cancellationToken)
    {
        IReadOnlyList<GoogleCalendarDescriptor> matches = findNameMatches(requestedName, calendars);
        List<GoogleCalendarDescriptor> resolvedMatches = new List<GoogleCalendarDescriptor>(matches.Count);
        foreach (GoogleCalendarDescriptor calendar in matches)
        {
            if (calendar.IsApplicationManaged
                || calendar.IsPrimary
                || calendar.CanWrite == false)
            {
                resolvedMatches.Add(calendar);
                continue;
            }

            PlanId? managedPlanIdOrNull = await mApiClient.FindManagedPlanIdAsync(accessToken, calendar.CalendarId, cancellationToken).ConfigureAwait(false);
            GoogleCalendarDescriptor resolvedCalendar = calendar;
            if (managedPlanIdOrNull.HasValue)
            {
                resolvedCalendar = calendar.WithManagedPlanId(managedPlanIdOrNull.Value);
            }

            resolvedMatches.Add(resolvedCalendar);
        }

        return resolvedMatches.AsReadOnly();
    }

    private static GoogleCalendarDescriptor? findSoleReplaceableCalendarOrNull(IReadOnlyList<GoogleCalendarDescriptor> matchingCalendars)
    {
        if (matchingCalendars.Count != 1 || matchingCalendars[0].CanReplace == false)
        {
            return null;
        }

        return matchingCalendars[0];
    }

    private static GoogleCalendarDescriptor? findCalendarByIdOrNull(GoogleCalendarId calendarId, IReadOnlyList<GoogleCalendarDescriptor> calendars)
    {
        foreach (GoogleCalendarDescriptor calendar in calendars)
        {
            if (calendar.CalendarId == calendarId)
            {
                return calendar;
            }
        }

        return null;
    }

    private static bool isSafeReplacementTarget(GoogleCalendarDescriptor currentCalendar, GoogleCalendarDescriptor confirmedCalendar, PlanName requestedName)
    {
        PlanName? displayNameOrNull = tryCreatePlanNameOrNull(currentCalendar.DisplayName);
        return currentCalendar.CalendarId == confirmedCalendar.CalendarId
            && currentCalendar.ManagedPlanIdOrNull == confirmedCalendar.ManagedPlanIdOrNull
            && currentCalendar.CanReplace
            && displayNameOrNull != null
            && CalendarNameConflictPolicy.IsSameName(requestedName, displayNameOrNull);
    }

    private static IReadOnlyList<GoogleCalendarDescriptor> findNameMatches(PlanName requestedName, IReadOnlyList<GoogleCalendarDescriptor> calendars)
    {
        List<GoogleCalendarDescriptor> matches = new List<GoogleCalendarDescriptor>();
        foreach (GoogleCalendarDescriptor calendar in calendars)
        {
            PlanName? displayNameOrNull = tryCreatePlanNameOrNull(calendar.DisplayName);
            if (displayNameOrNull != null && CalendarNameConflictPolicy.IsSameName(requestedName, displayNameOrNull))
            {
                matches.Add(calendar);
            }
        }

        return matches.AsReadOnly();
    }

    private static IReadOnlyList<PlanName> getExistingNames(IReadOnlyList<GoogleCalendarDescriptor> calendars)
    {
        List<PlanName> existingNames = new List<PlanName>(calendars.Count);
        foreach (GoogleCalendarDescriptor calendar in calendars)
        {
            PlanName? displayNameOrNull = tryCreatePlanNameOrNull(calendar.DisplayName);
            if (displayNameOrNull != null)
            {
                existingNames.Add(displayNameOrNull);
            }
        }

        return existingNames.AsReadOnly();
    }

    private static PlanName? tryCreatePlanNameOrNull(string value)
    {
        string normalizedValue = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalizedValue.Length == 0 || normalizedValue.Length > PlanName.MAXIMUM_LENGTH)
        {
            return null;
        }

        return new PlanName(normalizedValue);
    }

    private static GoogleCalendarExportResult mapAuthorizationFailure(GoogleOAuthAuthorizationResult authorizationResult)
    {
        EGoogleCalendarExportStatus status = authorizationResult.Status switch
        {
            EGoogleOAuthAuthorizationStatus.NotConfigured => EGoogleCalendarExportStatus.NotConfigured,
            EGoogleOAuthAuthorizationStatus.Cancelled => EGoogleCalendarExportStatus.AuthenticationCancelled,
            EGoogleOAuthAuthorizationStatus.Failed => EGoogleCalendarExportStatus.AuthenticationFailed,
            EGoogleOAuthAuthorizationStatus.NetworkFailed => EGoogleCalendarExportStatus.NetworkFailed,
            EGoogleOAuthAuthorizationStatus.None => throw new InvalidOperationException("Google Calendar authorization returned no status."),
            EGoogleOAuthAuthorizationStatus.Completed => throw new InvalidOperationException("Completed authorization cannot be mapped as a failure."),
            _ => throw new ArgumentOutOfRangeException(nameof(authorizationResult), authorizationResult.Status, "Unknown Google Calendar authorization status."),
        };
        return GoogleCalendarExportResult.Fail(status, authorizationResult.DiagnosticCodeOrNull);
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
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, mLifetimeCancellationSource.Token);
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

    private sealed class GoogleCalendarDestination
    {
        public GoogleCalendarExportPlan Plan { get; }

        public GoogleCalendarId? ExistingCalendarIdOrNull { get; }

        public PlanId? ReplacedPlanIdOrNull { get; }

        public PlanId PendingManagedPlanId
        {
            get
            {
                if (ReplacedPlanIdOrNull == null)
                {
                    return Plan.PlanId;
                }

                return ReplacedPlanIdOrNull.Value;
            }
        }

        private GoogleCalendarDestination(GoogleCalendarExportPlan plan, GoogleCalendarId? existingCalendarIdOrNull, PlanId? replacedPlanIdOrNull)
        {
            Plan = plan;
            ExistingCalendarIdOrNull = existingCalendarIdOrNull;
            ReplacedPlanIdOrNull = replacedPlanIdOrNull;
        }

        public static GoogleCalendarDestination CreateNew(GoogleCalendarExportPlan plan)
        {
            return new GoogleCalendarDestination(plan, null, null);
        }

        public static GoogleCalendarDestination Replace(GoogleCalendarExportPlan plan, GoogleCalendarId calendarId, PlanId replacedPlanId)
        {
            return new GoogleCalendarDestination(plan, calendarId, replacedPlanId);
        }
    }
}
