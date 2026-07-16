using System;
using System.Globalization;
using System.Text;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Tests.Product.Loading;

internal static class ProductWorkspaceLoaderTestData
{
    private const string COURSE_ID = "handong-global-university:CSE00001";
    private const string OFFERING_ID =
        "handong-global-university:2026-2:CSE00001:01";

    public static VerifiedCatalogPackage CreateCatalogPackage(
        CatalogRevision revision)
    {
        if (revision.IsValid == false)
        {
            throw new ArgumentException(
                "Test catalogs require a valid revision.",
                nameof(revision));
        }

        byte[] catalogBytes = createCatalogBytes(revision);
        byte[] indexBytes = createIndexBytes(revision, catalogBytes);
        return VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
    }

    public static PlanningWorkspace CreateEmptyWorkspace(CatalogRevision revision)
    {
        return createWorkspace(
            revision,
            Array.Empty<ScheduledCourseChoice>());
    }

    public static PlanningWorkspace CreateWorkspaceWithValidSelection(
        CatalogRevision revision)
    {
        ScheduledCourseChoice choice = new ScheduledCourseChoice(
            new CourseId(COURSE_ID),
            new OfferingId[] { new OfferingId(OFFERING_ID) });
        return createWorkspace(revision, new ScheduledCourseChoice[] { choice });
    }

    public static PlanningWorkspace CreateWorkspaceWithMissingOffering(
        CatalogRevision revision)
    {
        ScheduledCourseChoice choice = new ScheduledCourseChoice(
            new CourseId(COURSE_ID),
            new OfferingId[] { new OfferingId("missing-offering") });
        return createWorkspace(revision, new ScheduledCourseChoice[] { choice });
    }

    public static PlanningWorkspace CreateMixedBindingWorkspace()
    {
        PlanId firstPlanId = PlanId.CreateNew();
        PlanningPlan firstPlan = createEmptyPlan(
            firstPlanId,
            new PlanName("첫 번째 시간표"),
            new CatalogRevision(1));
        PlanId secondPlanId = PlanId.CreateNew();
        PlanningPlan secondPlan = createEmptyPlan(
            secondPlanId,
            new PlanName("두 번째 시간표"),
            new CatalogRevision(2));
        return new PlanningWorkspace(
            firstPlanId,
            new PlanningPlan[] { firstPlan, secondPlan });
    }

    private static byte[] createCatalogBytes(CatalogRevision revision)
    {
        string catalogId = createCatalogId(revision);
        string revisionText = revision.Value.ToString(CultureInfo.InvariantCulture);
        string json = $$"""
            {
              "documentType": "courseCatalog",
              "schemaVersion": 1,
              "catalogId": "{{catalogId}}",
              "revision": {{revisionText}},
              "institution": {
                "id": "handong-global-university",
                "name": {
                  "ko": "한동대학교",
                  "en": "Handong Global University"
                }
              },
              "term": {
                "id": "2026-2",
                "academicYear": 2026,
                "semester": 2
              },
              "source": {
                "providerId": "handong-global-university",
                "logicalFileName": "hgu-2026-2-source.xls",
                "declaredExtension": "xls",
                "detectedMediaType": "text/html",
                "declaredCharset": "ks_c_5601-1987",
                "decodedWith": "windows-949",
                "sizeBytes": 100,
                "sha256": "0000000000000000000000000000000000000000000000000000000000000000"
              },
              "converter": {
                "id": "handong-course-catalog-importer",
                "version": "1.0.0"
              },
              "counts": {
                "courses": 1,
                "offerings": 1,
                "scheduledOfferings": 1,
                "meetingNotProvided": 0
              },
              "dataQuality": {
                "scheduleNormalizationSource": "koreanPeriodText",
                "sourceEnglishScheduleMismatch": 0,
                "roomNotProvided": 0,
                "enrollmentNotProvided": 0,
                "instructorUnconfirmed": 0,
                "multiInstructorDisplay": 0,
                "sourceRemarkLookupOnly": 0,
                "manualReview": []
              },
              "courses": [
                {
                  "courseId": "{{COURSE_ID}}",
                  "code": "CSE00001",
                  "name": {
                    "ko": "자료구조",
                    "en": "Data Structures"
                  },
                  "credits": 3.0
                }
              ],
              "offerings": [
                {
                  "offeringId": "{{OFFERING_ID}}",
                  "courseId": "{{COURSE_ID}}",
                  "sectionCode": "01",
                  "requirementType": "majorRequired",
                  "offeringUnitName": "AI컴퓨터전자학부",
                  "instructionSession": "daytime",
                  "instructorAssignment": {
                    "status": "confirmed",
                    "displayText": "홍길동",
                    "additionalInstructorCount": 0
                  },
                  "schedule": {
                    "status": "scheduled",
                    "sourceTextKo": "월1",
                    "slots": [
                      {
                        "day": "monday",
                        "period": 1
                      }
                    ]
                  },
                  "location": {
                    "status": "assigned",
                    "displayText": "오석관 301"
                  },
                  "seatCapacity": 30,
                  "currentEnrollment": 20,
                  "englishInstructionPercentage": 0,
                  "generalEducationCategory": null,
                  "grading": {
                    "type": "letter",
                    "passFailOptionAvailable": false
                  },
                  "details": {
                    "syllabusUrl": null,
                    "remarksAvailable": false
                  },
                  "sourceRecordNumber": 2
                }
              ]
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] createIndexBytes(
        CatalogRevision revision,
        byte[] catalogBytes)
    {
        string catalogId = createCatalogId(revision);
        string revisionText = revision.Value.ToString(CultureInfo.InvariantCulture);
        CatalogFileSize fileSize = new CatalogFileSize(catalogBytes.LongLength);
        Sha256Digest sha256 = Sha256Digest.Compute(catalogBytes);
        string json = $$"""
            {
              "documentType": "courseCatalogIndex",
              "schemaVersion": 1,
              "defaultCatalogId": "{{catalogId}}",
              "catalogs": [
                {
                  "catalogId": "{{catalogId}}",
                  "catalogSchemaVersion": 1,
                  "institution": {
                    "id": "handong-global-university",
                    "name": {
                      "ko": "한동대학교",
                      "en": "Handong Global University"
                    }
                  },
                  "term": {
                    "id": "2026-2",
                    "academicYear": 2026,
                    "semester": 2
                  },
                  "revision": {{revisionText}},
                  "file": {
                    "relativePath": "handong-global-university/2026-2/catalog-{{revision.FileComponent}}.json",
                    "mediaType": "application/json",
                    "charset": "utf-8",
                    "contentEncoding": "identity",
                    "sizeBytes": {{fileSize.Value}},
                    "sha256": "{{sha256.HexValue}}"
                  },
                  "counts": {
                    "courses": 1,
                    "offerings": 1
                  }
                }
              ]
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    private static PlanningWorkspace createWorkspace(
        CatalogRevision revision,
        ScheduledCourseChoice[] choices)
    {
        PlanId planId = PlanId.CreateNew();
        PlanningPlan plan = new PlanningPlan(
            planId,
            new PlanName("저장된 시간표"),
            createCatalogBinding(revision),
            choices,
            Array.Empty<UnscheduledOfferingSelection>());
        return new PlanningWorkspace(planId, new PlanningPlan[] { plan });
    }

    private static PlanningPlan createEmptyPlan(
        PlanId planId,
        PlanName planName,
        CatalogRevision revision)
    {
        return new PlanningPlan(
            planId,
            planName,
            createCatalogBinding(revision),
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
    }

    private static PlanCatalogBinding createCatalogBinding(CatalogRevision revision)
    {
        return new PlanCatalogBinding(
            new CatalogId(createCatalogId(revision)),
            AcademicTerm.Parse("2026-2"),
            revision);
    }

    private static string createCatalogId(CatalogRevision revision)
    {
        return "handong-global-university:2026-2:" + revision.FileComponent;
    }
}
