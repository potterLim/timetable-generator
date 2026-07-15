using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal static class HandongExportSchema
{
    public const int COLUMN_COUNT = 16;
    public const string DECLARED_CHARSET = "ks_c_5601-1987";

    private static readonly IReadOnlyList<EHandongColumn> COLUMNS =
        new ReadOnlyCollection<EHandongColumn>(
            new EHandongColumn[]
            {
                EHandongColumn.Classification,
                EHandongColumn.CourseCode,
                EHandongColumn.Section,
                EHandongColumn.CourseName,
                EHandongColumn.Credits,
                EHandongColumn.OfferingInformation,
                EHandongColumn.Period,
                EHandongColumn.Classroom,
                EHandongColumn.Capacity,
                EHandongColumn.Enrollment,
                EHandongColumn.EnglishInstruction,
                EHandongColumn.GeneralEducationPractical,
                EHandongColumn.GradingType,
                EHandongColumn.PassFailAvailable,
                EHandongColumn.Syllabus,
                EHandongColumn.Notes,
            });

    public static IReadOnlyList<EHandongColumn> Columns
    {
        get
        {
            return COLUMNS;
        }
    }

    public static int GetColumnIndex(EHandongColumn column)
    {
        switch (column)
        {
            case EHandongColumn.Classification:
                return 0;
            case EHandongColumn.CourseCode:
                return 1;
            case EHandongColumn.Section:
                return 2;
            case EHandongColumn.CourseName:
                return 3;
            case EHandongColumn.Credits:
                return 4;
            case EHandongColumn.OfferingInformation:
                return 5;
            case EHandongColumn.Period:
                return 6;
            case EHandongColumn.Classroom:
                return 7;
            case EHandongColumn.Capacity:
                return 8;
            case EHandongColumn.Enrollment:
                return 9;
            case EHandongColumn.EnglishInstruction:
                return 10;
            case EHandongColumn.GeneralEducationPractical:
                return 11;
            case EHandongColumn.GradingType:
                return 12;
            case EHandongColumn.PassFailAvailable:
                return 13;
            case EHandongColumn.Syllabus:
                return 14;
            case EHandongColumn.Notes:
                return 15;
            default:
                Debug.Fail("Unexpected Handong column: " + column);
                throw new ArgumentOutOfRangeException(nameof(column), column, "The Handong column is not supported.");
        }
    }

    public static string GetExpectedHeaderText(EHandongColumn column)
    {
        switch (column)
        {
            case EHandongColumn.Classification:
                return "구분";
            case EHandongColumn.CourseCode:
                return "과목코드";
            case EHandongColumn.Section:
                return "분반";
            case EHandongColumn.CourseName:
                return "과목명(CourseName)";
            case EHandongColumn.Credits:
                return "학점";
            case EHandongColumn.OfferingInformation:
                return "개설정보";
            case EHandongColumn.Period:
                return "시간(Period)";
            case EHandongColumn.Classroom:
                return "강의실";
            case EHandongColumn.Capacity:
                return "정원";
            case EHandongColumn.Enrollment:
                return "인원";
            case EHandongColumn.EnglishInstruction:
                return "영어";
            case EHandongColumn.GeneralEducationPractical:
                return "교양실무";
            case EHandongColumn.GradingType:
                return "성적유형";
            case EHandongColumn.PassFailAvailable:
                return "PF병행";
            case EHandongColumn.Syllabus:
                return "강의계획서";
            case EHandongColumn.Notes:
                return "비고";
            default:
                Debug.Fail("Unexpected Handong column: " + column);
                throw new ArgumentOutOfRangeException(nameof(column), column, "The Handong column is not supported.");
        }
    }

    public static bool IsExpectedHeader(
        EHandongColumn column,
        IReadOnlyList<string> headerLines)
    {
        string actualHeaderText = GetCanonicalHeaderText(headerLines);
        string expectedHeaderText = GetExpectedHeaderText(column);
        return string.Equals(actualHeaderText, expectedHeaderText, StringComparison.Ordinal);
    }

    public static string GetCanonicalHeaderText(IReadOnlyList<string> headerLines)
    {
        ArgumentNullException.ThrowIfNull(headerLines);

        StringBuilder canonicalTextBuilder = new StringBuilder();
        foreach (string headerLine in headerLines)
        {
            canonicalTextBuilder.Append(headerLine);
        }

        return canonicalTextBuilder.ToString();
    }
}
