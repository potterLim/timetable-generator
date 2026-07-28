using System;
using System.Globalization;
using System.Net;
using AngleSharp.Dom;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal static class HandongSourceLinkMetadataReader
{
    private const string COURSE_CODE_PARAMETER_NAME = "kang_gwamok_code";
    private const string COURSE_SECTION_PARAMETER_NAME = "kang_bunban";
    private const string ACADEMIC_YEAR_PARAMETER_NAME = "kang_yy";
    private const string ACADEMIC_SEMESTER_PARAMETER_NAME = "kang_hakgi";

    public static HandongSourceLinkMetadata? ReadMetadataOrNull(IElement rowElement, SourceRecordNumber sourceRecordNumber)
    {
        ArgumentNullException.ThrowIfNull(rowElement);

        HandongSourceLinkMetadata? sourceLinkMetadataOrNull = null;
        foreach (IElement linkElement in rowElement.QuerySelectorAll("a[href]"))
        {
            string? linkTargetOrNull = linkElement.GetAttribute("href");
            if (isHandongOfferingLink(linkTargetOrNull) == false)
            {
                continue;
            }

            HandongSourceLinkMetadata currentMetadata = readMetadata(linkTargetOrNull!, sourceRecordNumber);

            if (sourceLinkMetadataOrNull != null && sourceLinkMetadataOrNull != currentMetadata)
            {
                throw new HandongSourceFormatException("Source record " + sourceRecordNumber + " contains conflicting Handong offering-link metadata.");
            }

            sourceLinkMetadataOrNull = currentMetadata;
        }

        return sourceLinkMetadataOrNull;
    }

    private static bool isHandongOfferingLink(string? linkTargetOrNull)
    {
        if (string.IsNullOrEmpty(linkTargetOrNull))
        {
            return false;
        }

        return linkTargetOrNull.Contains(COURSE_CODE_PARAMETER_NAME + "=", StringComparison.OrdinalIgnoreCase);
    }

    private static HandongSourceLinkMetadata readMetadata(string linkTarget, SourceRecordNumber sourceRecordNumber)
    {
        try
        {
            string courseCodeValue = readRequiredParameter(linkTarget, COURSE_CODE_PARAMETER_NAME, sourceRecordNumber);
            string courseSectionValue = readRequiredParameter(linkTarget, COURSE_SECTION_PARAMETER_NAME, sourceRecordNumber);
            string academicYearText = readRequiredParameter(linkTarget, ACADEMIC_YEAR_PARAMETER_NAME, sourceRecordNumber);
            string academicSemesterText = readRequiredParameter(linkTarget, ACADEMIC_SEMESTER_PARAMETER_NAME, sourceRecordNumber);

            int academicYearValue = parseIntegerParameter(academicYearText, ACADEMIC_YEAR_PARAMETER_NAME, sourceRecordNumber);
            int academicSemesterValue = parseIntegerParameter(academicSemesterText, ACADEMIC_SEMESTER_PARAMETER_NAME, sourceRecordNumber);

            AcademicTerm academicTerm = new AcademicTerm(new AcademicYear(academicYearValue), new AcademicSemester(academicSemesterValue));

            return new HandongSourceLinkMetadata(academicTerm, new CourseCode(courseCodeValue), new CourseSectionCode(courseSectionValue));
        }
        catch (HandongSourceFormatException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new HandongSourceFormatException("Source record " + sourceRecordNumber + " contains invalid Handong offering-link metadata.", exception);
        }
    }

    private static string readRequiredParameter(string linkTarget, string parameterName, SourceRecordNumber sourceRecordNumber)
    {
        string queryMarker = "?" + parameterName + "=";
        string additionalMarker = "&" + parameterName + "=";

        int parameterMarkerIndex = linkTarget.IndexOf(queryMarker, StringComparison.OrdinalIgnoreCase);
        int parameterValueIndex;
        if (parameterMarkerIndex >= 0)
        {
            parameterValueIndex = parameterMarkerIndex + queryMarker.Length;
        }
        else
        {
            parameterMarkerIndex = linkTarget.IndexOf(additionalMarker, StringComparison.OrdinalIgnoreCase);
            if (parameterMarkerIndex < 0)
            {
                throw new HandongSourceFormatException("Source record " + sourceRecordNumber + " is missing offering-link parameter '" + parameterName + "'.");
            }

            parameterValueIndex = parameterMarkerIndex + additionalMarker.Length;
        }

        int parameterValueEndIndex = findParameterValueEndIndex(linkTarget, parameterValueIndex);
        string encodedParameterValue = linkTarget.Substring(parameterValueIndex, parameterValueEndIndex - parameterValueIndex);
        string decodedParameterValue = WebUtility.UrlDecode(encodedParameterValue).Trim();
        if (decodedParameterValue.Length == 0)
        {
            throw new HandongSourceFormatException("Source record " + sourceRecordNumber + " has an empty offering-link parameter '" + parameterName + "'.");
        }

        return decodedParameterValue;
    }

    private static int findParameterValueEndIndex(string linkTarget, int parameterValueIndex)
    {
        for (int characterIndex = parameterValueIndex; characterIndex < linkTarget.Length; ++characterIndex)
        {
            char character = linkTarget[characterIndex];
            if (character == '&' || character == '\'' || character == '"' || character == ')')
            {
                return characterIndex;
            }
        }

        return linkTarget.Length;
    }

    private static int parseIntegerParameter(string parameterText, string parameterName, SourceRecordNumber sourceRecordNumber)
    {
        int parameterValue;
        bool isParameterParsed = int.TryParse(parameterText, NumberStyles.None, CultureInfo.InvariantCulture, out parameterValue);
        if (isParameterParsed == false)
        {
            throw new HandongSourceFormatException("Source record " + sourceRecordNumber + " has a non-numeric offering-link parameter '" + parameterName + "'.");
        }

        return parameterValue;
    }
}
