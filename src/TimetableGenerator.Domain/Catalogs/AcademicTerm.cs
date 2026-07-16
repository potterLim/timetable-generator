using System;
using System.Globalization;

namespace TimetableGenerator.Domain.Catalogs;

public readonly record struct AcademicTerm
{
    private const char TERM_SEPARATOR = '-';

    public AcademicYear AcademicYear { get; }

    public AcademicSemester Semester { get; }

    public bool IsValid
    {
        get
        {
            return AcademicYear.IsValid && Semester.IsValid;
        }
    }

    public string Id
    {
        get
        {
            return AcademicYear + TERM_SEPARATOR.ToString() + Semester;
        }
    }

    public AcademicTerm(AcademicYear academicYear, AcademicSemester semester)
    {
        if (academicYear.IsValid == false)
        {
            throw new ArgumentException(
                "Academic terms require a valid academic year.",
                nameof(academicYear));
        }

        if (semester.IsValid == false)
        {
            throw new ArgumentException(
                "Academic terms require a valid academic semester.",
                nameof(semester));
        }

        AcademicYear = academicYear;
        Semester = semester;
    }

    public static AcademicTerm Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Academic terms cannot be empty.");
        }

        string[] parts = value.Trim().Split(TERM_SEPARATOR);
        if (parts.Length != 2)
        {
            throw new FormatException("Academic terms must use the YYYY-S format.");
        }

        int academicYearValue;
        bool isAcademicYearParsed = int.TryParse(
            parts[0],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out academicYearValue);
        int semesterValue;
        bool isSemesterParsed = int.TryParse(
            parts[1],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out semesterValue);
        if (isAcademicYearParsed == false || isSemesterParsed == false)
        {
            throw new FormatException(
                "Academic terms must contain numeric year and semester values.");
        }

        return new AcademicTerm(
            new AcademicYear(academicYearValue),
            new AcademicSemester(semesterValue));
    }

    public override string ToString()
    {
        return Id;
    }
}
