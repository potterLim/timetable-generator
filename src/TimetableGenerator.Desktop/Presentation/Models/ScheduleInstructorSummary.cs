using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed record ScheduleInstructorSummary
{
    public string Value { get; }

    public EInstructorAssignmentStatus AssignmentStatus { get; }

    public bool IsConfirmed
    {
        get
        {
            return AssignmentStatus == EInstructorAssignmentStatus.Confirmed;
        }
    }

    public ScheduleInstructorSummary(InstructorAssignmentMetadata instructorAssignment)
    {
        if (instructorAssignment == null)
        {
            throw new ArgumentNullException(nameof(instructorAssignment));
        }

        Value = CatalogSummaryFormatter.FormatInstructorSummary(instructorAssignment);
        AssignmentStatus = instructorAssignment.Status;
    }

    public override string ToString()
    {
        return Value;
    }
}
