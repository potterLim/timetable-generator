using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class ValidatedScheduleChoice
{
    private readonly IReadOnlyList<ScheduledOffering> mOfferings;

    public IReadOnlyList<ScheduledOffering> Offerings
    {
        get
        {
            return mOfferings;
        }
    }

    public ValidatedScheduleChoice(IEnumerable<ScheduledOffering> offerings)
    {
        if (offerings == null)
        {
            throw new ArgumentNullException(nameof(offerings));
        }

        List<ScheduledOffering> copiedOfferings = new List<ScheduledOffering>();
        foreach (ScheduledOffering offering in offerings)
        {
            if (offering == null)
            {
                throw new ArgumentException(
                    "Validated schedule choices cannot contain null offerings.",
                    nameof(offerings));
            }

            copiedOfferings.Add(offering);
        }

        if (copiedOfferings.Count == 0)
        {
            throw new ArgumentException(
                "Validated schedule choices require at least one offering.",
                nameof(offerings));
        }

        mOfferings = copiedOfferings.AsReadOnly();
    }
}
