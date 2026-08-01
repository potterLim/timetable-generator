using System;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public void ExportResultRejectsInvalidStatusAndNegativeCounts()
    {
        Assert.Throws<ArgumentException>(
            delegate
            {
                GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.None, "invalid");
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new GoogleCalendarReconciliationResult(-1, 0, 0);
            });
    }

    [Fact]
    public void CalendarDescriptorRejectsInvalidStronglyTypedState()
    {
        Assert.Throws<ArgumentException>(
            delegate
            {
                new GoogleCalendarDescriptor(
                    new GoogleCalendarId("calendar-id"),
                    "시간표",
                    false,
                    default(PlanId),
                    EGoogleCalendarAccessRole.Owner);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new GoogleCalendarDescriptor(
                    new GoogleCalendarId("calendar-id"),
                    "시간표",
                    false,
                    null,
                    (EGoogleCalendarAccessRole)99);
            });
    }
}
