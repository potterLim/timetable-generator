using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct AcademicTerm
{
    private const char TERM_SEPARATOR = '-';

    public AcademicYear AcademicYear { get; }
    public AcademicSemester Semester { get; }

    public string Id
    {
        get
        {
            return AcademicYear + TERM_SEPARATOR.ToString() + Semester;
        }
    }

    public AcademicTerm(AcademicYear academicYear, AcademicSemester semester)
    {
        AcademicYear = academicYear;
        Semester = semester;
    }

    public static AcademicTerm Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("The academic term cannot be empty.");
        }

        string[] parts = value.Trim().Split(TERM_SEPARATOR);
        if (parts.Length != 2)
        {
            throw new FormatException("The academic term must use the YYYY-S format.");
        }

        int academicYearValue;
        bool isAcademicYearParsed = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out academicYearValue);
        int semesterValue;
        bool isSemesterParsed = int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out semesterValue);
        if (isAcademicYearParsed == false || isSemesterParsed == false)
        {
            throw new FormatException("The academic term must contain numeric year and semester values.");
        }

        return new AcademicTerm(new AcademicYear(academicYearValue), new AcademicSemester(semesterValue));
    }

    public override string ToString()
    {
        return Id;
    }
}
