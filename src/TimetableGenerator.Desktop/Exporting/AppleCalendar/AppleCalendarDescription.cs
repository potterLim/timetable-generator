using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed record AppleCalendarDescription
{
    public string Value { get; }

    private AppleCalendarDescription(string value)
    {
        Value = value;
    }

    public static AppleCalendarDescription Create(
        InstitutionName institutionName,
        AcademicTerm academicTerm)
    {
        if (institutionName == null)
        {
            throw new ArgumentNullException(nameof(institutionName));
        }

        if (academicTerm.IsValid == false)
        {
            throw new ArgumentException(
                "Apple Calendar descriptions require a valid academic term.",
                nameof(academicTerm));
        }

        return new AppleCalendarDescription(institutionName.Value + " " + academicTerm.Id + " 시간표입니다.");
    }

    public override string ToString()
    {
        return Value;
    }
}
