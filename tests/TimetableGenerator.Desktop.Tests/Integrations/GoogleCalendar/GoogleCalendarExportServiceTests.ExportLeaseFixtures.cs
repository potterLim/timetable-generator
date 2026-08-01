using System;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    private sealed class FixedAccessTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(GoogleOAuthAuthorizationResult.Complete(new GoogleAccessToken("access-secret")));
        }
    }

    private sealed class CountingAccessTokenProvider : IGoogleAccessTokenProvider
    {
        private int mRequestCount;

        public int RequestCount
        {
            get
            {
                return Volatile.Read(ref mRequestCount);
            }
        }

        public Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref mRequestCount);
            return Task.FromResult(GoogleOAuthAuthorizationResult.Fail(EGoogleOAuthAuthorizationStatus.NotConfigured, "oauth_client_not_configured"));
        }
    }

    private sealed class SequencedAccessTokenProvider : IGoogleAccessTokenProvider
    {
        private int mRequestCount;

        public Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int requestCount = Interlocked.Increment(ref mRequestCount);
            if (requestCount == 1)
            {
                return Task.FromException<GoogleOAuthAuthorizationResult>(new InvalidOperationException("Simulated authorization infrastructure failure."));
            }

            return Task.FromResult(GoogleOAuthAuthorizationResult.Fail(EGoogleOAuthAuthorizationStatus.NotConfigured, "oauth_client_not_configured"));
        }
    }

    private sealed class TrackingExportLeaseProvider : IGoogleCalendarExportLeaseProvider
    {
        private int mAcquireCount;
        private int mReleaseCount;

        public int AcquireCount
        {
            get
            {
                return Volatile.Read(ref mAcquireCount);
            }
        }

        public int ReleaseCount
        {
            get
            {
                return Volatile.Read(ref mReleaseCount);
            }
        }

        public Task<IGoogleCalendarExportLease> AcquireAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref mAcquireCount);
            return Task.FromResult<IGoogleCalendarExportLease>(new TrackingExportLease(this));
        }

        private sealed class TrackingExportLease : IGoogleCalendarExportLease
        {
            private TrackingExportLeaseProvider? mOwnerOrNull;

            public TrackingExportLease(TrackingExportLeaseProvider owner)
            {
                mOwnerOrNull = owner;
            }

            public ValueTask DisposeAsync()
            {
                TrackingExportLeaseProvider? ownerOrNull = Interlocked.Exchange(ref mOwnerOrNull, null);
                if (ownerOrNull != null)
                {
                    Interlocked.Increment(ref ownerOrNull.mReleaseCount);
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
