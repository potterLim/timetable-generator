using System;
using System.Collections.Generic;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongCatalogNormalizer
{
    private readonly HandongOfferingNormalizer mOfferingNormalizer;

    public HandongCatalogNormalizer()
    {
        mOfferingNormalizer = new HandongOfferingNormalizer();
    }

    public CourseCatalog NormalizeCatalog(HandongExportDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        Dictionary<CourseCode, CatalogCourse> coursesByCode = new Dictionary<CourseCode, CatalogCourse>();
        Dictionary<CourseOfferingKey, CatalogOffering> offeringsByKey = new Dictionary<CourseOfferingKey, CatalogOffering>();

        int englishScheduleMismatchCount = 0;
        int roomNotProvidedCount = 0;
        int enrollmentNotProvidedCount = 0;
        int instructorUnconfirmedCount = 0;
        int multiInstructorDisplayCount = 0;
        int sourceRemarkLookupOnlyCount = 0;
        List<CatalogManualReview> manualReviews = new List<CatalogManualReview>();

        foreach (HandongRawOfferingRow row in document.Rows)
        {
            HandongOfferingNormalizationResult normalizationResult = mOfferingNormalizer.NormalizeOffering(row);
            addOrValidateCourse(coursesByCode, normalizationResult.Course, manualReviews);
            addOffering(offeringsByKey, normalizationResult.Offering);

            if (normalizationResult.EnglishScheduleComparison ==
                EEnglishScheduleComparison.DiffersFromKoreanSchedule)
            {
                ++englishScheduleMismatchCount;
            }

            CatalogOffering offering = normalizationResult.Offering;
            if (offering.Logistics.Location.Status == ELocationAssignmentStatus.NotProvided)
            {
                ++roomNotProvidedCount;
            }

            if (offering.Capacity.Enrollment.Status == EEnrollmentStatus.NotProvided)
            {
                ++enrollmentNotProvidedCount;
            }

            InstructorAssignment instructorAssignment = offering.Instruction.InstructorAssignment;
            if (instructorAssignment.Status == EInstructorAssignmentStatus.Unconfirmed)
            {
                ++instructorUnconfirmedCount;
            }

            if (instructorAssignment.Status == EInstructorAssignmentStatus.Confirmed &&
                instructorAssignment.GetAdditionalInstructorCount().Value > 0)
            {
                ++multiInstructorDisplayCount;
            }

            if (offering.Details.RemarksAvailability == ERemarksAvailability.LookupAvailable)
            {
                ++sourceRemarkLookupOnlyCount;
            }
        }

        List<CatalogCourse> courses = new List<CatalogCourse>(coursesByCode.Values);
        courses.Sort(compareCourses);
        List<CatalogOffering> offerings = new List<CatalogOffering>(offeringsByKey.Values);
        offerings.Sort(compareOfferings);
        manualReviews.Sort(compareManualReviews);

        CatalogDataQuality dataQuality = new CatalogDataQuality(
            EScheduleNormalizationSource.KoreanPeriodText,
            new CatalogItemCount(englishScheduleMismatchCount),
            new CatalogItemCount(roomNotProvidedCount),
            new CatalogItemCount(enrollmentNotProvidedCount),
            new CatalogItemCount(instructorUnconfirmedCount),
            new CatalogItemCount(multiInstructorDisplayCount),
            new CatalogItemCount(sourceRemarkLookupOnlyCount),
            manualReviews);
        return new CourseCatalog(courses, offerings, dataQuality);
    }

    private static void addOrValidateCourse(
        IDictionary<CourseCode, CatalogCourse> coursesByCode,
        CatalogCourse candidateCourse,
        ICollection<CatalogManualReview> manualReviews)
    {
        CatalogCourse? existingCourseOrNull;
        bool hasExistingCourse = coursesByCode.TryGetValue(candidateCourse.Code, out existingCourseOrNull);
        if (hasExistingCourse)
        {
            if (existingCourseOrNull == null)
            {
                throw new InvalidOperationException(
                    "The course lookup returned a null course for an existing key.");
            }

            validateConsistentCourse(existingCourseOrNull, candidateCourse);
            return;
        }

        coursesByCode.Add(candidateCourse.Code, candidateCourse);
        if (candidateCourse.EnglishName.Value.Contains('?', StringComparison.Ordinal))
        {
            CatalogManualReview manualReview = new CatalogManualReview(
                candidateCourse.Code,
                EManualReviewField.EnglishCourseName,
                EManualReviewReason.UnexpectedQuestionMarkInSource,
                new ManualReviewSourceValue(candidateCourse.EnglishName.Value));
            manualReviews.Add(manualReview);
        }
    }

    private static void validateConsistentCourse(
        CatalogCourse existingCourse,
        CatalogCourse candidateCourse)
    {
        if (existingCourse.KoreanName != candidateCourse.KoreanName)
        {
            throw createCourseConflict(existingCourse, candidateCourse, ECourseDefinitionField.KoreanName);
        }

        if (existingCourse.EnglishName != candidateCourse.EnglishName)
        {
            throw createCourseConflict(existingCourse, candidateCourse, ECourseDefinitionField.EnglishName);
        }

        if (existingCourse.Credits != candidateCourse.Credits)
        {
            throw createCourseConflict(existingCourse, candidateCourse, ECourseDefinitionField.Credits);
        }
    }

    private static ConflictingCourseDefinitionException createCourseConflict(
        CatalogCourse existingCourse,
        CatalogCourse candidateCourse,
        ECourseDefinitionField field)
    {
        return new ConflictingCourseDefinitionException(
            existingCourse.Code,
            field,
            existingCourse.FirstSourceRecordNumber,
            candidateCourse.FirstSourceRecordNumber);
    }

    private static void addOffering(
        IDictionary<CourseOfferingKey, CatalogOffering> offeringsByKey,
        CatalogOffering offering)
    {
        CatalogOffering? existingOfferingOrNull;
        bool hasExistingOffering = offeringsByKey.TryGetValue(offering.Key, out existingOfferingOrNull);
        if (hasExistingOffering)
        {
            if (existingOfferingOrNull == null)
            {
                throw new InvalidOperationException(
                    "The offering lookup returned a null offering for an existing key.");
            }

            throw new DuplicateCourseOfferingException(
                offering.Key,
                existingOfferingOrNull.SourceRecordNumber,
                offering.SourceRecordNumber);
        }

        offeringsByKey.Add(offering.Key, offering);
    }

    private static int compareCourses(CatalogCourse left, CatalogCourse right)
    {
        return string.Compare(left.Code.Value, right.Code.Value, StringComparison.Ordinal);
    }

    private static int compareOfferings(CatalogOffering left, CatalogOffering right)
    {
        int courseCodeComparison = string.Compare(
            left.Key.CourseCode.Value,
            right.Key.CourseCode.Value,
            StringComparison.Ordinal);
        if (courseCodeComparison != 0)
        {
            return courseCodeComparison;
        }

        return string.Compare(
            left.Key.SectionCode.Value,
            right.Key.SectionCode.Value,
            StringComparison.Ordinal);
    }

    private static int compareManualReviews(CatalogManualReview left, CatalogManualReview right)
    {
        int courseCodeComparison = string.Compare(
            left.CourseCode.Value,
            right.CourseCode.Value,
            StringComparison.Ordinal);
        if (courseCodeComparison != 0)
        {
            return courseCodeComparison;
        }

        return left.Field.CompareTo(right.Field);
    }
}
