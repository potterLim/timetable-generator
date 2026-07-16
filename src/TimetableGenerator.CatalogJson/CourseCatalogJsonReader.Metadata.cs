using System;
using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.CatalogJson.Internal;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public static partial class CourseCatalogJsonReader
{
    private static CatalogSourceMetadata parseSource(
        JsonElement element,
        string path,
        InstitutionId expectedProviderId,
        AcademicTerm term)
    {
        StrictJsonObject sourceObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "providerId",
                "logicalFileName",
                "declaredExtension",
                "detectedMediaType",
                "declaredCharset",
                "decodedWith",
                "sizeBytes",
                "sha256",
            });
        InstitutionId providerId = new InstitutionId(sourceObject.GetString("providerId"));
        if (providerId != expectedProviderId)
        {
            throw new CatalogJsonFormatException(
                sourceObject.GetPropertyPath("providerId"),
                "the source provider must match the catalog institution.");
        }

        string logicalFileName = sourceObject.GetString("logicalFileName");
        string expectedLogicalFileName = "hgu-" + term.Id + "-source.xls";
        CatalogJsonValueParser.RequireExactString(
            logicalFileName,
            expectedLogicalFileName,
            sourceObject.GetPropertyPath("logicalFileName"));
        string declaredExtension = sourceObject.GetString("declaredExtension");
        string detectedMediaType = sourceObject.GetString("detectedMediaType");
        string declaredCharset = sourceObject.GetString("declaredCharset");
        string decodedWith = sourceObject.GetString("decodedWith");
        CatalogJsonValueParser.RequireExactString(
            declaredExtension,
            "xls",
            sourceObject.GetPropertyPath("declaredExtension"));
        CatalogJsonValueParser.RequireExactString(
            detectedMediaType,
            "text/html",
            sourceObject.GetPropertyPath("detectedMediaType"));
        if (string.IsNullOrWhiteSpace(declaredCharset))
        {
            throw new CatalogJsonFormatException(
                sourceObject.GetPropertyPath("declaredCharset"),
                "the declared source charset cannot be empty.");
        }

        CatalogJsonValueParser.RequireExactString(
            decodedWith,
            "windows-949",
            sourceObject.GetPropertyPath("decodedWith"));
        CatalogFileSize size = new CatalogFileSize(sourceObject.GetInt64("sizeBytes"));
        Sha256Digest sha256 = new Sha256Digest(sourceObject.GetString("sha256"));
        return new CatalogSourceMetadata(
            providerId,
            logicalFileName,
            declaredExtension,
            detectedMediaType,
            declaredCharset,
            decodedWith,
            size,
            sha256);
    }

    private static CatalogConverterMetadata parseConverter(JsonElement element, string path)
    {
        StrictJsonObject converterObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "id",
                "version",
            });
        string converterId = converterObject.GetString("id");
        CatalogJsonValueParser.RequireExactString(
            converterId,
            "handong-course-catalog-importer",
            converterObject.GetPropertyPath("id"));
        string converterVersion = converterObject.GetString("version");
        Version? parsedVersionOrNull;
        bool isVersion = Version.TryParse(converterVersion, out parsedVersionOrNull);
        if (isVersion == false || parsedVersionOrNull == null)
        {
            throw new CatalogJsonFormatException(
                converterObject.GetPropertyPath("version"),
                "a numeric converter version is required.");
        }

        return new CatalogConverterMetadata(converterId, converterVersion);
    }

    private static CatalogDocumentCounts parseDocumentCounts(JsonElement element, string path)
    {
        StrictJsonObject countsObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "courses",
                "offerings",
                "scheduledOfferings",
                "meetingNotProvided",
            });
        return new CatalogDocumentCounts(
            countsObject.GetInt32("courses"),
            countsObject.GetInt32("offerings"),
            countsObject.GetInt32("scheduledOfferings"),
            countsObject.GetInt32("meetingNotProvided"));
    }

    private static CatalogDataQualityMetadata parseDataQuality(
        JsonElement element,
        string path,
        IEnumerable<CourseId> knownCourseIds)
    {
        StrictJsonObject dataQualityObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "scheduleNormalizationSource",
                "sourceEnglishScheduleMismatch",
                "roomNotProvided",
                "enrollmentNotProvided",
                "instructorUnconfirmed",
                "multiInstructorDisplay",
                "sourceRemarkLookupOnly",
                "manualReview",
            });
        EScheduleNormalizationSource normalizationSource = parseScheduleNormalizationSource(
            dataQualityObject.GetString("scheduleNormalizationSource"),
            dataQualityObject.GetPropertyPath("scheduleNormalizationSource"));
        List<CatalogManualReview> manualReviews = parseManualReviews(
            dataQualityObject.GetArray("manualReview"),
            knownCourseIds);
        return new CatalogDataQualityMetadata(
            normalizationSource,
            dataQualityObject.GetInt32("sourceEnglishScheduleMismatch"),
            dataQualityObject.GetInt32("roomNotProvided"),
            dataQualityObject.GetInt32("enrollmentNotProvided"),
            dataQualityObject.GetInt32("instructorUnconfirmed"),
            dataQualityObject.GetInt32("multiInstructorDisplay"),
            dataQualityObject.GetInt32("sourceRemarkLookupOnly"),
            manualReviews);
    }

    private static List<CatalogManualReview> parseManualReviews(
        JsonElement manualReviewsElement,
        IEnumerable<CourseId> knownCourseIds)
    {
        HashSet<CourseId> knownCourseIdSet = new HashSet<CourseId>(knownCourseIds);
        List<CatalogManualReview> manualReviews = new List<CatalogManualReview>();
        int reviewIndex = 0;
        foreach (JsonElement reviewElement in manualReviewsElement.EnumerateArray())
        {
            string reviewPath = "$.dataQuality.manualReview[" + reviewIndex + "]";
            StrictJsonObject reviewObject = StrictJsonObject.Create(
                reviewElement,
                reviewPath,
                new string[]
                {
                    "courseId",
                    "field",
                    "reason",
                    "sourceValue",
                });
            CourseId courseId = new CourseId(reviewObject.GetString("courseId"));
            if (knownCourseIdSet.Contains(courseId) == false)
            {
                throw new CatalogJsonFormatException(
                    reviewObject.GetPropertyPath("courseId"),
                    "manual reviews must reference a course in the catalog.");
            }

            EManualReviewField field = parseManualReviewField(
                reviewObject.GetString("field"),
                reviewObject.GetPropertyPath("field"));
            EManualReviewReason reason = parseManualReviewReason(
                reviewObject.GetString("reason"),
                reviewObject.GetPropertyPath("reason"));
            manualReviews.Add(new CatalogManualReview(
                courseId,
                field,
                reason,
                reviewObject.GetString("sourceValue")));
            ++reviewIndex;
        }

        return manualReviews;
    }

    private static EScheduleNormalizationSource parseScheduleNormalizationSource(
        string value,
        string path)
    {
        switch (value)
        {
            case "koreanPeriodText":
                return EScheduleNormalizationSource.KoreanPeriodText;
            default:
                throw new CatalogJsonFormatException(
                    path,
                    "the schedule normalization source is not supported by schema v1.");
        }
    }

    private static EManualReviewField parseManualReviewField(string value, string path)
    {
        switch (value)
        {
            case "name.en":
                return EManualReviewField.EnglishCourseName;
            default:
                throw new CatalogJsonFormatException(
                    path,
                    "the manual review field is not supported by schema v1.");
        }
    }

    private static EManualReviewReason parseManualReviewReason(string value, string path)
    {
        switch (value)
        {
            case "unexpectedQuestionMarkInSource":
                return EManualReviewReason.UnexpectedQuestionMarkInSource;
            default:
                throw new CatalogJsonFormatException(
                    path,
                    "the manual review reason is not supported by schema v1.");
        }
    }
}
