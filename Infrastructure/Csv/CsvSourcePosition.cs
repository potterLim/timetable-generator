using System;

namespace TimetableGenerator.Infrastructure.Csv;

public readonly record struct CsvSourcePosition
{
    private readonly CsvRowNumber mRowNumber;

    public static CsvSourcePosition File
    {
        get
        {
            return default(CsvSourcePosition);
        }
    }

    public bool HasRowNumber
    {
        get
        {
            return mRowNumber.IsValid;
        }
    }

    private CsvSourcePosition(CsvRowNumber rowNumber)
    {
        mRowNumber = rowNumber;
    }

    public static CsvSourcePosition CreateAtRow(CsvRowNumber rowNumber)
    {
        if (rowNumber.IsValid == false)
        {
            throw new ArgumentException("A valid CSV row number is required.", nameof(rowNumber));
        }

        return new CsvSourcePosition(rowNumber);
    }

    public CsvRowNumber GetRowNumber()
    {
        if (HasRowNumber == false)
        {
            throw new InvalidOperationException("This CSV diagnostic does not refer to a row.");
        }

        return mRowNumber;
    }
}
