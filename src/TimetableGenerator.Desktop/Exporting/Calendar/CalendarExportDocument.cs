using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class CalendarExportDocument
{
    private readonly IReadOnlyList<RecurringCalendarEvent> mEvents;

    public PlanId PlanId { get; }

    public PlanName CalendarName { get; }

    public InstitutionName InstitutionName { get; }

    public AcademicTermCalendarMetadata AcademicCalendar { get; }

    public IReadOnlyList<RecurringCalendarEvent> Events
    {
        get
        {
            return mEvents;
        }
    }

    public CalendarExportDocument(
        PlanId planId,
        PlanName calendarName,
        InstitutionName institutionName,
        AcademicTermCalendarMetadata academicCalendar,
        IEnumerable<RecurringCalendarEvent> events)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException("Calendar export documents require a valid plan ID.", nameof(planId));
        }

        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        if (institutionName == null)
        {
            throw new ArgumentNullException(nameof(institutionName));
        }

        if (academicCalendar == null)
        {
            throw new ArgumentNullException(nameof(academicCalendar));
        }

        if (events == null)
        {
            throw new ArgumentNullException(nameof(events));
        }

        PlanId = planId;
        CalendarName = calendarName;
        InstitutionName = institutionName;
        AcademicCalendar = academicCalendar;
        mEvents = copyAndValidateEvents(events);
    }

    private static IReadOnlyList<RecurringCalendarEvent> copyAndValidateEvents(IEnumerable<RecurringCalendarEvent> events)
    {
        List<RecurringCalendarEvent> copiedEvents = new List<RecurringCalendarEvent>();
        HashSet<CalendarEventUid> uniqueUids = new HashSet<CalendarEventUid>();
        foreach (RecurringCalendarEvent calendarEvent in events)
        {
            if (calendarEvent == null)
            {
                throw new ArgumentException("Calendar export documents cannot contain null events.", nameof(events));
            }

            if (uniqueUids.Add(calendarEvent.Uid) == false)
            {
                throw new ArgumentException("Calendar export documents cannot contain duplicate event UIDs.", nameof(events));
            }

            copiedEvents.Add(calendarEvent);
        }

        return copiedEvents.AsReadOnly();
    }
}
