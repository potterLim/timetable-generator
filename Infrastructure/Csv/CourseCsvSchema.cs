using System;

namespace TimetableGenerator.Infrastructure.Csv;

internal sealed class CourseCsvSchema
{
    public int ColumnCount { get; }

    public bool HasClassroomLocationColumn { get; }

    private CourseCsvSchema(int columnCount)
    {
        if (columnCount != 4 && columnCount != 5)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        ColumnCount = columnCount;
        HasClassroomLocationColumn = columnCount == 5;
    }

    public static CourseCsvSchema CreateWithoutClassroomLocation()
    {
        return new CourseCsvSchema(4);
    }

    public static CourseCsvSchema CreateWithClassroomLocation()
    {
        return new CourseCsvSchema(5);
    }
}
