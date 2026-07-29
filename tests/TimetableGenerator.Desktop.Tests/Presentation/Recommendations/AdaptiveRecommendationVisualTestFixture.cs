using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Tests.Presentation.Recommendations;

internal static class AdaptiveRecommendationVisualTestFixture
{
    private const int COURSE_COUNT = 5;

    private const int OFFERING_COUNT_PER_COURSE = 2;

    private const int EXPECTED_RECOMMENDATION_COUNT = 32;

    public static PlannerWorkspaceViewModel CreateWorkspace(
        out ControlledExhaustiveScheduleRecommendationProvider recommendationProvider)
    {
        CourseCatalogDocument document = createDocument();
        PlanningWorkspace workspace = createWorkspace(document);
        verifyRecommendationCount(document.Catalog, workspace.GetActivePlan());
        recommendationProvider = new ControlledExhaustiveScheduleRecommendationProvider(document.Catalog, EControlledExhaustiveOutcome.WaitForCancellation);
        RecommendationCalculationPolicy calculationPolicy = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(24), TimeSpan.Zero);
        return PlannerWorkspaceTestFactory.CreateWorkspace(
            document,
            workspace,
            recommendationProvider,
            calculationPolicy);
    }

    private static CourseCatalogDocument createDocument()
    {
        CourseCatalogDocument template = Presentation.Catalog.CatalogProjectionTestFixture.CreateDocument();
        InstitutionId institutionId = template.Catalog.InstitutionId;
        List<CatalogCourse> courses = new List<CatalogCourse>(COURSE_COUNT);
        List<CatalogOffering> offerings = new List<CatalogOffering>(COURSE_COUNT * OFFERING_COUNT_PER_COURSE);
        List<CatalogOfferingMetadata> metadata = new List<CatalogOfferingMetadata>(COURSE_COUNT * OFFERING_COUNT_PER_COURSE);

        for (int courseIndex = 0; courseIndex < COURSE_COUNT; ++courseIndex)
        {
            CourseId courseId = new CourseId("adaptive-course-" + courseIndex);
            CatalogCourse course = new CatalogCourse(
                courseId,
                new CourseCode("TST10" + (courseIndex + 1).ToString("D3", CultureInfo.InvariantCulture)),
                new KoreanCourseName("적응형 시간표 과목 " + (courseIndex + 1)),
                new EnglishCourseName("Adaptive Schedule Course " + (courseIndex + 1)),
                new CourseCredits(3m));
            courses.Add(course);

            for (int offeringIndex = 0; offeringIndex < OFFERING_COUNT_PER_COURSE; ++offeringIndex)
            {
                OfferingId offeringId = new OfferingId("adaptive-offering-" + courseIndex + "-" + offeringIndex);
                EDay day = offeringIndex == 0 ? EDay.Monday : EDay.Tuesday;
                AcademicPeriod period = new AcademicPeriod(courseIndex + 1);
                CatalogOffering offering = new CatalogOffering(
                    offeringId,
                    courseId,
                    new CourseSectionCode((offeringIndex + 1).ToString("D2", CultureInfo.InvariantCulture)),
                    MeetingSchedule.CreateScheduled(
                        new MeetingSlot[]
                        {
                            new MeetingSlot(day, period),
                        }));
                offerings.Add(offering);
                metadata.Add(createOfferingMetadata(offeringId, day, period, metadata.Count + 1));
            }
        }

        CourseCatalog catalog = new CourseCatalog(
            new CatalogId("adaptive-visual-test:2026-2:r0001"),
            institutionId,
            template.Catalog.InstitutionName,
            template.Catalog.Term,
            template.Catalog.Revision,
            courses,
            offerings);
        CatalogDocumentCounts counts = new CatalogDocumentCounts(
            new CatalogCourseCount(courses.Count),
            new CatalogOfferingCount(offerings.Count),
            new CatalogScheduledOfferingCount(offerings.Count),
            new CatalogMeetingNotProvidedCount(0));
        CatalogDataQualityMetadata dataQuality = new CatalogDataQualityMetadata(
            EScheduleNormalizationSource.KoreanPeriodText,
            new CatalogSourceEnglishScheduleMismatchCount(0),
            new CatalogRoomNotProvidedCount(0),
            new CatalogEnrollmentNotProvidedCount(offerings.Count),
            new CatalogInstructorUnconfirmedCount(0),
            new CatalogMultiInstructorDisplayCount(0),
            new CatalogSourceRemarkLookupOnlyCount(0),
            Array.Empty<CatalogManualReview>());
        return new CourseCatalogDocument(
            catalog,
            template.Institution,
            template.Source,
            template.Converter,
            counts,
            dataQuality,
            metadata);
    }

    private static PlanningWorkspace createWorkspace(CourseCatalogDocument document)
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            document.Catalog.Id,
            document.Catalog.InstitutionId,
            document.Catalog.Term,
            document.Catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
        List<CourseChoiceGroup> choiceGroups = new List<CourseChoiceGroup>(COURSE_COUNT);
        for (int courseIndex = 0; courseIndex < COURSE_COUNT; ++courseIndex)
        {
            CourseId courseId = new CourseId("adaptive-course-" + courseIndex);
            choiceGroups.Add(
                CourseChoiceGroup.CreateWithAcceptableOfferings(
                    CourseChoiceGroupId.CreateNew(),
                    courseId,
                    new OfferingId[]
                    {
                        new OfferingId("adaptive-offering-" + courseIndex + "-0"),
                        new OfferingId("adaptive-offering-" + courseIndex + "-1"),
                    }));
        }

        PlanId planId = PlanId.CreateNew();
        PlanningPlan plan = new PlanningPlan(
            planId,
            new PlanName("2026-2학기 시간표"),
            binding,
            new PlanningPlanContent(
                choiceGroups,
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(binding, planId, new PlanningPlan[] { plan });
    }

    private static void verifyRecommendationCount(CourseCatalog catalog, PlanningPlan plan)
    {
        CatalogScheduleRecommendationProvider provider = new CatalogScheduleRecommendationProvider(catalog);
        ScheduleRecommendationResult result = provider.Generate(plan, ScheduleRecommendationLimit.Unlimited, CancellationToken.None);
        if (result.Completion != EScheduleRecommendationCompletion.Completed
            || result.Recommendations.Count != EXPECTED_RECOMMENDATION_COUNT)
        {
            throw new InvalidOperationException("The adaptive recommendation visual fixture must produce exactly 32 recommendations.");
        }
    }

    private static CatalogOfferingMetadata createOfferingMetadata(
        OfferingId offeringId,
        EDay day,
        AcademicPeriod period,
        int sourceRecordNumber)
    {
        CatalogOfferingClassificationMetadata classification = CatalogOfferingClassificationMetadata.CreateWithoutGeneralEducationCategory(
            ERequirementType.MajorElective,
            new OfferingUnitName("테스트학부"),
            EInstructionSession.Daytime);
        CatalogOfferingInstructionMetadata instruction = new CatalogOfferingInstructionMetadata(
            InstructorAssignmentMetadata.CreateConfirmed(
                new InstructorDisplayText("담당 교수"),
                new AdditionalInstructorCount(0)),
            new EnglishInstructionPercentage(0m),
            new GradingMetadata(EGradingType.Letter, EPassFailOptionAvailability.Unavailable));
        string dayText = day == EDay.Monday ? "월" : "화";
        CatalogOfferingLogisticsMetadata logistics = CatalogOfferingLogisticsMetadata.CreateScheduled(
            new KoreanScheduleSourceText(dayText + period.Value),
            LocationAssignmentMetadata.CreateAssigned(new ClassroomDisplayText("오석관 " + (201 + sourceRecordNumber))));
        return new CatalogOfferingMetadata(
            offeringId,
            classification,
            instruction,
            logistics,
            CatalogOfferingCapacityMetadata.CreateWithoutCurrentEnrollment(new OfferingSeatCapacity(30)),
            new OfferingDetailsMetadata(ERemarksAvailability.Unavailable),
            new SourceRecordNumber(sourceRecordNumber));
    }
}
