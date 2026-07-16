using System;
using System.Globalization;

namespace TimetableGenerator.Domain.Catalogs;

public readonly record struct AcademicSemester
{
    private const int FIRST_SEMESTER = 1;
    private const int SECOND_SEMESTER = 2;

    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value == FIRST_SEMESTER || Value == SECOND_SEMESTER;
        }
    }

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
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
