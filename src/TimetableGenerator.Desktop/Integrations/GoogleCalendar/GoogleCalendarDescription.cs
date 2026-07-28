using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarDescription
{
    public string Value { get; }

    private GoogleCalendarDescription(string value)
    {
        Value = value;
    }

    public static GoogleCalendarDescription Create(InstitutionName institutionName, AcademicTerm academicTerm)
    {
        if (institutionName == null)
        {
            throw new ArgumentNullException(nameof(institutionName));
        }

        if (academicTerm.IsValid == false)
        {
            throw new ArgumentException("Google Calendar descriptions require a valid academic term.", nameof(academicTerm));
        }

        return new GoogleCalendarDescription(institutionName.Value + " " + academicTerm.Id + " 시간표입니다.");
    }

    public override string ToString()
    {
        return Value;
    }
}
