using System;
using System.Collections.Generic;
using System.Net;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarPaginationGuard
{
    private readonly string mDiagnosticCode;
    private readonly int mMaximumPageCount;
    private readonly HashSet<string> mVisitedPageTokens;

    private int mRequestedPageCount;

    public GoogleCalendarPaginationGuard(int maximumPageCount, string diagnosticCode)
    {
        if (maximumPageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPageCount));
        }

        if (string.IsNullOrWhiteSpace(diagnosticCode))
        {
            throw new ArgumentException("Pagination guards require a diagnostic code.", nameof(diagnosticCode));
        }

        mMaximumPageCount = maximumPageCount;
        mDiagnosticCode = diagnosticCode;
        mVisitedPageTokens = new HashSet<string>(StringComparer.Ordinal);
    }

    public void BeginPage()
    {
        mRequestedPageCount++;
        if (mRequestedPageCount > mMaximumPageCount)
        {
            throw createProtocolException();
        }
    }

    public string? AcceptNextPageTokenOrNull(string? pageTokenOrNull)
    {
        if (string.IsNullOrWhiteSpace(pageTokenOrNull))
        {
            return null;
        }

        if (mVisitedPageTokens.Add(pageTokenOrNull) == false)
        {
            throw createProtocolException();
        }

        return pageTokenOrNull;
    }

    private GoogleCalendarApiException createProtocolException()
    {
        return new GoogleCalendarApiException(HttpStatusCode.OK, mDiagnosticCode);
    }
}
