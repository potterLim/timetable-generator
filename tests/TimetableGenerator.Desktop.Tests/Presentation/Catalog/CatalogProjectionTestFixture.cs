using System;
using System.Collections.Generic;
using System.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Tests.Presentation.Catalog;

internal static class CatalogProjectionTestFixture
{
    public static CourseCatalogDocument CreateDocument()
    {
        MeetingSchedule primarySchedule = MeetingSchedule.CreateScheduled(
            new MeetingSlot[]
            {
                new MeetingSlot(EDay.Wednesday, new AcademicPeriod(2)),
                new MeetingSlot(EDay.Monday, new AcademicPeriod(1)),
            });
        return createDocument(primarySchedule, ECatalogCourseOrder.Original);
    }

    public static CourseCatalogDocument CreateDocumentWithChangedPrimarySchedule()
    {
        MeetingSchedule changedSchedule = MeetingSchedule.CreateScheduled(
            new MeetingSlot[]
            {
                new MeetingSlot(EDay.Monday, new AcademicPeriod(4)),
            });
        return createDocument(changedSchedule, ECatalogCourseOrder.Original);
    }

    public static CourseCatalogDocument CreateReorderedDocument()
    {
        MeetingSchedule primarySchedule = MeetingSchedule.CreateScheduled(
            new MeetingSlot[]
            {
                new MeetingSlot(EDay.Wednesday, new AcademicPeriod(2)),
                new MeetingSlot(EDay.Monday, new AcademicPeriod(1)),
            });
        return createDocument(primarySchedule, ECatalogCourseOrder.Reversed);
    }

    public static ScheduleRecommendation CreateRecommendation(CourseCatalogDocument document)
    {
        CourseId programmingCourseId = new CourseId("course-programming");
        OfferingId primaryOfferingId = new OfferingId("offering-programming-primary");
        CourseId seminarCourseId = new CourseId("course-seminar");
        OfferingId seminarOfferingId = new OfferingId("offering-seminar-unscheduled");

        ScheduledCourseChoice scheduledChoice = new ScheduledCourseChoice(
            programmingCourseId,
            new OfferingId[] { primaryOfferingId });
        UnscheduledOfferingSelection unscheduledSelection =
            new UnscheduledOfferingSelection(seminarCourseId, seminarOfferingId);
        PlanCatalogBinding catalogBinding = new PlanCatalogBinding(
            document.Catalog.Id,
            document.Catalog.InstitutionId,
            document.Catalog.Term,
            document.Catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningPlan plan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("프로젝션 테스트"),
            catalogBinding,
            new ScheduledCourseChoice[] { scheduledChoice },
            new UnscheduledOfferingSelection[] { unscheduledSelection });
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            document.Catalog,
            plan,
            new ScheduleRecommendationLimit(1));

        ScheduleRecommendationGenerator generator = new ScheduleRecommendationGenerator();
        ScheduleRecommendationResult result = generator.GenerateRecommendations(
            request,
            CancellationToken.None);
        if (result.IsSuccessful == false || result.Recommendations.Count != 1)
        {
            throw new InvalidOperationException(
                "The catalog projection recommendation fixture could not be generated.");
        }

        return result.Recommendations[0];
    }

    private static CourseCatalogDocument createDocument(
        MeetingSchedule primarySchedule,
        ECatalogCourseOrder courseOrder)
    {
        InstitutionId institutionId = new InstitutionId("handong-global-university");
        InstitutionName institutionName = new InstitutionName("한동대학교");
        CatalogCourse programmingCourse = createProgrammingCourse();
        CatalogCourse seminarCourse = createSeminarCourse();

        CatalogOffering primaryOffering = new CatalogOffering(
            new OfferingId("offering-programming-primary"),
            programmingCourse.Id,
            new CourseSectionCode("01"),
            primarySchedule);
        CatalogOffering alternativeOffering = new CatalogOffering(
            new OfferingId("offering-programming-alternative"),
            programmingCourse.Id,
            new CourseSectionCode("02"),
            MeetingSchedule.CreateScheduled(
                new MeetingSlot[]
                {
                    new MeetingSlot(EDay.Tuesday, new AcademicPeriod(3)),
                }));
        CatalogOffering seminarOffering = new CatalogOffering(
            new OfferingId("offering-seminar-unscheduled"),
            seminarCourse.Id,
            new CourseSectionCode("01"),
            MeetingSchedule.NotProvided);
        CatalogOffering secondSeminarOffering = new CatalogOffering(
            new OfferingId("offering-seminar-unscheduled-02"),
            seminarCourse.Id,
            new CourseSectionCode("02"),
            MeetingSchedule.NotProvided);

        IReadOnlyList<CatalogCourse> courses = createCourseOrder(
            programmingCourse,
            seminarCourse,
            courseOrder);
        IReadOnlyList<CatalogOffering> offerings = createOfferingOrder(
            primaryOffering,
            alternativeOffering,
            seminarOffering,
            secondSeminarOffering,
            courseOrder);
        CourseCatalog catalog = new CourseCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            institutionId,
            institutionName,
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            courses,
            offerings);

        List<CatalogOfferingMetadata> metadata = new List<CatalogOfferingMetadata>();
        metadata.Add(createPrimaryOfferingMetadata(primaryOffering.Id));
        metadata.Add(createAlternativeOfferingMetadata(alternativeOffering.Id));
        metadata.Add(createSeminarOfferingMetadata(seminarOffering.Id));
        metadata.Add(createSecondSeminarOfferingMetadata(secondSeminarOffering.Id));

        InstitutionMetadata institution = new InstitutionMetadata(
            institutionId,
            institutionName,
            new EnglishInstitutionName("Handong Global University"));
        CatalogSourceMetadata source = new CatalogSourceMetadata(
            institutionId,
            new CatalogSourceLogicalFileName("catalog.xls"),
            new CatalogFileExtension(".xls"),
            new CatalogMediaType("application/vnd.ms-excel"),
            new CatalogCharset("windows-949"),
            new CatalogDecoderName("windows-949"),
            new CatalogFileSize(1),
            new Sha256Digest(new string('0', 64)));
        CatalogConverterMetadata converter = new CatalogConverterMetadata(
            new CatalogConverterId("catalog-converter"),
            new CatalogConverterVersion(new Version(1, 0, 0)));
        CatalogDocumentCounts counts = new CatalogDocumentCounts(
            new CatalogCourseCount(2),
            new CatalogOfferingCount(4),
            new CatalogScheduledOfferingCount(2),
            new CatalogMeetingNotProvidedCount(2));
        CatalogDataQualityMetadata dataQuality = new CatalogDataQualityMetadata(
            EScheduleNormalizationSource.KoreanPeriodText,
            new CatalogSourceEnglishScheduleMismatchCount(0),
            new CatalogRoomNotProvidedCount(3),
            new CatalogEnrollmentNotProvidedCount(4),
            new CatalogInstructorUnconfirmedCount(1),
            new CatalogMultiInstructorDisplayCount(1),
            new CatalogSourceRemarkLookupOnlyCount(0),
            Array.Empty<CatalogManualReview>());

        return new CourseCatalogDocument(
            catalog,
            institution,
            source,
            converter,
            counts,
            dataQuality,
            metadata);
    }

    private static CatalogCourse createProgrammingCourse()
    {
        return new CatalogCourse(
            new CourseId("course-programming"),
            new CourseCode("CSE10001"),
            new KoreanCourseName("프로그래밍 I"),
            new EnglishCourseName("Programming I"),
            new CourseCredits(3m));
    }

    private static CatalogCourse createSeminarCourse()
    {
        return new CatalogCourse(
            new CourseId("course-seminar"),
            new CourseCode("BFT30009"),
            new KoreanCourseName("세미나 3"),
            new EnglishCourseName("Seminar 3"),
            new CourseCredits(1m));
    }

    private static IReadOnlyList<CatalogCourse> createCourseOrder(
        CatalogCourse programmingCourse,
        CatalogCourse seminarCourse,
        ECatalogCourseOrder courseOrder)
    {
        if (courseOrder == ECatalogCourseOrder.Reversed)
        {
            return new CatalogCourse[] { seminarCourse, programmingCourse };
        }

        return new CatalogCourse[] { programmingCourse, seminarCourse };
    }

    private static IReadOnlyList<CatalogOffering> createOfferingOrder(
        CatalogOffering primaryOffering,
        CatalogOffering alternativeOffering,
        CatalogOffering seminarOffering,
        CatalogOffering secondSeminarOffering,
        ECatalogCourseOrder courseOrder)
    {
        if (courseOrder == ECatalogCourseOrder.Reversed)
        {
            return new CatalogOffering[]
            {
                seminarOffering,
                secondSeminarOffering,
                alternativeOffering,
                primaryOffering,
            };
        }

        return new CatalogOffering[]
        {
            primaryOffering,
            alternativeOffering,
            seminarOffering,
            secondSeminarOffering,
        };
    }

    private static CatalogOfferingMetadata createPrimaryOfferingMetadata(
        OfferingId offeringId)
    {
        InstructorAssignmentMetadata instructor = InstructorAssignmentMetadata.CreateConfirmed(
            new InstructorDisplayText("홍길동 외 1명"),
            new AdditionalInstructorCount(1));
        LocationAssignmentMetadata location = LocationAssignmentMetadata.CreateAssigned(
            new ClassroomDisplayText("오석관 301"));
        return createScheduledMetadata(
            offeringId,
            ERequirementType.MajorRequired,
            new OfferingUnitName("전산전자공학부"),
            instructor,
            location,
            new KoreanScheduleSourceText("월1, 수2"),
            new SourceRecordNumber(1));
    }

    private static CatalogOfferingMetadata createAlternativeOfferingMetadata(
        OfferingId offeringId)
    {
        return createScheduledMetadata(
            offeringId,
            ERequirementType.GeneralElective,
            new OfferingUnitName("ICT창업학부"),
            InstructorAssignmentMetadata.NotProvided,
            LocationAssignmentMetadata.NotProvided,
            new KoreanScheduleSourceText("화3"),
            new SourceRecordNumber(2));
    }

    private static CatalogOfferingMetadata createSeminarOfferingMetadata(
        OfferingId offeringId)
    {
        CatalogOfferingClassificationMetadata classification =
            CatalogOfferingClassificationMetadata.CreateWithoutGeneralEducationCategory(
                ERequirementType.GeneralElective,
                new OfferingUnitName("ICT창업학부"),
                EInstructionSession.Daytime);
        CatalogOfferingInstructionMetadata instruction = createInstruction(
            InstructorAssignmentMetadata.Unconfirmed);
        CatalogOfferingLogisticsMetadata logistics =
            CatalogOfferingLogisticsMetadata.CreateWithoutProvidedSchedule(
                LocationAssignmentMetadata.NotProvided);
        return createMetadata(
            offeringId,
            classification,
            instruction,
            logistics,
            new SourceRecordNumber(3));
    }

    private static CatalogOfferingMetadata createSecondSeminarOfferingMetadata(
        OfferingId offeringId)
    {
        CatalogOfferingClassificationMetadata classification =
            CatalogOfferingClassificationMetadata.CreateWithoutGeneralEducationCategory(
                ERequirementType.GeneralElective,
                new OfferingUnitName("ICT창업학부"),
                EInstructionSession.Daytime);
        CatalogOfferingInstructionMetadata instruction = createInstruction(
            InstructorAssignmentMetadata.NotProvided);
        CatalogOfferingLogisticsMetadata logistics =
            CatalogOfferingLogisticsMetadata.CreateWithoutProvidedSchedule(
                LocationAssignmentMetadata.NotProvided);
        return createMetadata(
            offeringId,
            classification,
            instruction,
            logistics,
            new SourceRecordNumber(4));
    }

    private static CatalogOfferingMetadata createScheduledMetadata(
        OfferingId offeringId,
        ERequirementType requirementType,
        OfferingUnitName offeringUnitName,
        InstructorAssignmentMetadata instructor,
        LocationAssignmentMetadata location,
        KoreanScheduleSourceText scheduleSourceText,
        SourceRecordNumber sourceRecordNumber)
    {
        CatalogOfferingClassificationMetadata classification =
            CatalogOfferingClassificationMetadata.CreateWithoutGeneralEducationCategory(
                requirementType,
                offeringUnitName,
                EInstructionSession.Daytime);
        CatalogOfferingInstructionMetadata instruction = createInstruction(instructor);
        CatalogOfferingLogisticsMetadata logistics =
            CatalogOfferingLogisticsMetadata.CreateScheduled(scheduleSourceText, location);
        return createMetadata(
            offeringId,
            classification,
            instruction,
            logistics,
            sourceRecordNumber);
    }

    private static CatalogOfferingInstructionMetadata createInstruction(
        InstructorAssignmentMetadata instructor)
    {
        return new CatalogOfferingInstructionMetadata(
            instructor,
            new EnglishInstructionPercentage(0m),
            new GradingMetadata(
                EGradingType.Letter,
                EPassFailOptionAvailability.Unavailable));
    }

    private static CatalogOfferingMetadata createMetadata(
        OfferingId offeringId,
        CatalogOfferingClassificationMetadata classification,
        CatalogOfferingInstructionMetadata instruction,
        CatalogOfferingLogisticsMetadata logistics,
        SourceRecordNumber sourceRecordNumber)
    {
        return new CatalogOfferingMetadata(
            offeringId,
            classification,
            instruction,
            logistics,
            CatalogOfferingCapacityMetadata.CreateWithoutCurrentEnrollment(
                new OfferingSeatCapacity(30)),
            new OfferingDetailsMetadata(ERemarksAvailability.Unavailable),
            sourceRecordNumber);
    }
}
