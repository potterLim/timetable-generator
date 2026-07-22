using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class CalendarEventProjectionGroup
{
    private readonly HashSet<EDay> mDays;

    public CalendarEventProjectionGroupKey Key { get; }

    public CalendarEventContent Content { get; }

    public CalendarEventProjectionGroup(
        CalendarEventProjectionGroupKey key,
        CalendarEventContent content)
    {
        if (key.SourceIdentity.IsValid == false || key.TimeRange.IsValid == false)
        {
            throw new ArgumentException("Calendar projection groups require a valid key.", nameof(key));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        Key = key;
        Content = content;
        mDays = new HashSet<EDay>();
    }

    public void AddDay(EDay day, CalendarEventContent content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (hasSameContent(content) == false)
        {
            throw new ArgumentException(
                "Grouped calendar entries must have matching event content.",
                nameof(content));
        }

        if (mDays.Add(day) == false)
        {
            throw new ArgumentException(
                "A grouped calendar event cannot repeat the same weekday.",
                nameof(day));
        }
    }

    public RecurringCalendarEvent CreateEvent(PlanId planId)
    {
        CalendarEventUid uid = CalendarEventUidFactory.Create(planId, Key.SourceIdentity, Key.TimeRange);
        return new RecurringCalendarEvent(uid, Content, Key.TimeRange, mDays);
    }

    private bool hasSameContent(CalendarEventContent content)
    {
        return string.Equals(
            Content.Summary,
            content.Summary,
            StringComparison.Ordinal)
            && string.Equals(
                Content.Location,
                content.Location,
                StringComparison.Ordinal)
            && string.Equals(
                Content.Description,
                content.Description,
                StringComparison.Ordinal);
    }
}
