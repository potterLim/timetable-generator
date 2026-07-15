using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct AcademicSemester
{
    private const int FIRST_SEMESTER = 1;
    private const int SECOND_SEMESTER = 2;

    public int Value { get; }

    public AcademicSemester(int value)
    {
        if (value < FIRST_SEMESTER || value > SECOND_SEMESTER)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Only the first and second academic semesters are supported.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
