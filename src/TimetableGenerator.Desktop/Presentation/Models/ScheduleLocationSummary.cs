using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed record ScheduleLocationSummary
{
    public string Value { get; }

    public ELocationAssignmentStatus AssignmentStatus { get; }

    public bool IsAssigned
    {
        get
        {
            return AssignmentStatus == ELocationAssignmentStatus.Assigned;
        }
    }

    public ScheduleLocationSummary(LocationAssignmentMetadata locationAssignment)
    {
        if (locationAssignment == null)
        {
            throw new ArgumentNullException(nameof(locationAssignment));
        }

        Value = CatalogSummaryFormatter.FormatLocationSummary(locationAssignment);
        AssignmentStatus = locationAssignment.Status;
    }

    public override string ToString()
    {
        return Value;
    }
}
