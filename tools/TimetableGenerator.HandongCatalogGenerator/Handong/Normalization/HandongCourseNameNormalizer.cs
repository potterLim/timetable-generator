using System.Collections.Generic;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongCourseNameNormalizer
{
    public HandongCourseNameNormalizationResult NormalizeCourseName(HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.CourseName);
        if (lines.Count < 2)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.CourseName,
                "Korean and English course names are required.");
        }

        string englishNameValue = HandongCellValueReader.getCombinedText(lines, 1);
        bool hasSourceWrapper = englishNameValue.Length >= 2
            && englishNameValue[0] == '('
            && englishNameValue[englishNameValue.Length - 1] == ')';
        if (hasSourceWrapper == false)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.CourseName,
                "The English course name must use the source's outer parentheses.");
        }

        englishNameValue = englishNameValue.Substring(1, englishNameValue.Length - 2).Trim();

        KoreanCourseName koreanName = new KoreanCourseName(lines[0]);
        EnglishCourseName englishName = new EnglishCourseName(englishNameValue);
        return new HandongCourseNameNormalizationResult(koreanName, englishName);
    }
}
