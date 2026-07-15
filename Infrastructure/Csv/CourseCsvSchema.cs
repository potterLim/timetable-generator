using System;

namespace TimetableGenerator.Infrastructure.Csv;

internal sealed class CourseCsvSchema
{
    private const int COLUMN_COUNT_WITHOUT_CLASSROOM_LOCATION = 4;
    private const int COLUMN_COUNT_WITH_CLASSROOM_LOCATION = 5;

    public int ColumnCount { get; }

    public bool HasClassroomLocationColumn { get; }

    private CourseCsvSchema(int columnCount)
    {
        if (columnCount != COLUMN_COUNT_WITHOUT_CLASSROOM_LOCATION &&
            columnCount != COLUMN_COUNT_WITH_CLASSROOM_LOCATION)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        ColumnCount = columnCount;
        HasClassroomLocationColumn = columnCount == COLUMN_COUNT_WITH_CLASSROOM_LOCATION;
    }

    public static CourseCsvSchema CreateWithoutClassroomLocation()
    {
        return new CourseCsvSchema(COLUMN_COUNT_WITHOUT_CLASSROOM_LOCATION);
    }

    public static CourseCsvSchema CreateWithClassroomLocation()
    {
        return new CourseCsvSchema(COLUMN_COUNT_WITH_CLASSROOM_LOCATION);
    }
}
