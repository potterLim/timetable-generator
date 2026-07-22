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
    private static readonly InstitutionId DEFAULT_INSTITUTION_ID = new InstitutionId("handong-global-university");
    private static readonly AcademicTerm DEFAULT_TERM = AcademicTerm.Parse("2026-2");
    private static readonly CourseCode DEFAULT_COURSE_CODE = new CourseCode("CSE00001");

    private const string COURSE_ID = "handong-global-university:CSE00001";
    private const string OFFERING_ID = "handong-global-university:2026-2:CSE00001:01";

    public static VerifiedCatalogPackage CreateCatalogPackage(CatalogRevision revision)
    {
        return createCatalogPackage(revision, DEFAULT_INSTITUTION_ID, DEFAULT_TERM, DEFAULT_COURSE_CODE);
    }

    public static VerifiedCatalogPackage CreateCatalogPackage(
        CatalogRevision revision,
        InstitutionId institutionId,
        AcademicTerm term)
    {
        return createCatalogPackage(revision, institutionId, term, DEFAULT_COURSE_CODE);
    }

    public static VerifiedCatalogPackage CreateCatalogPackageWithoutSavedCourse(
        CatalogRevision revision)
    {
        return createCatalogPackage(
            revision,
            DEFAULT_INSTITUTION_ID,
            DEFAULT_TERM,
            new CourseCode("CSE99999"));
    }

    public static PlanningWorkspace CreateEmptyWorkspace(CatalogRevision revision)
    {
        return createWorkspace(revision, Array.Empty<CourseChoiceGroup>());
    }

    public static PlanningWorkspace CreateWorkspaceWithValidSelection(CatalogRevision revision)
    {
        CourseChoiceGroup choiceGroup = createCourseChoiceGroup(new OfferingId(OFFERING_ID));
        return createWorkspace(
            revision,
            new CourseChoiceGroup[] { choiceGroup });
    }

    public static PlanningWorkspace CreateWorkspaceWithMissingOffering(CatalogRevision revision)
    {
        CourseChoiceGroup choiceGroup = createCourseChoiceGroup(new OfferingId("missing-offering"));
        return createWorkspace(
            revision,
            new CourseChoiceGroup[] { choiceGroup });
    }

    public static PlanningWorkspace CreateWorkspaceWithoutPlans(CatalogRevision revision)
    {
        return new PlanningWorkspace(createCatalogBinding(revision), null, Array.Empty<PlanningPlan>());
    }

    private static VerifiedCatalogPackage createCatalogPackage(
        CatalogRevision revision,
        InstitutionId institutionId,
        AcademicTerm term,
        CourseCode courseCode)
    {
        if (revision.IsValid == false)
        {
            throw new ArgumentException("Test catalogs require a valid revision.", nameof(revision));
        }

        if (institutionId == null)
        {
            throw new ArgumentNullException(nameof(institutionId));
        }

        if (term.IsValid == false)
        {
            throw new ArgumentException("Test catalogs require a valid academic term.", nameof(term));
        }

        if (courseCode == null)
        {
            throw new ArgumentNullException(nameof(courseCode));
        }

        byte[] catalogBytes = createCatalogBytes(revision, institutionId, term, courseCode);
        byte[] indexBytes = createIndexBytes(revision, institutionId, term, catalogBytes);
        return VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
    }

    private static byte[] createCatalogBytes(
        CatalogRevision revision,
        InstitutionId institutionId,
        AcademicTerm term,
        CourseCode courseCode)
    {
        string catalogId = createCatalogId(revision, institutionId, term);
        string courseId = institutionId.Value + ":" + courseCode.Value;
        string offeringId = institutionId.Value
            + ":"
            + term.Id
            + ":"
            + courseCode.Value
            + ":01";
        string revisionText = revision.Value.ToString(CultureInfo.InvariantCulture);
        string academicYearText = term.AcademicYear.Value.ToString(CultureInfo.InvariantCulture);
        string semesterText = term.Semester.Value.ToString(CultureInfo.InvariantCulture);
        string json = $$"""
            {
              "documentType": "courseCatalog",
              "schemaVersion": 1,
              "catalogId": "{{catalogId}}",
              "revision": {{revisionText}},
              "institution": {
                "id": "{{institutionId.Value}}",
                "name": {
                  "ko": "한동대학교",
                  "en": "Handong Global University"
                }
              },
              "term": {
                "id": "{{term.Id}}",
                "academicYear": {{academicYearText}},
                "semester": {{semesterText}}
              },
              "source": {
                "providerId": "{{institutionId.Value}}",
                "logicalFileName": "hgu-{{term.Id}}-source.xls",
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
                  "courseId": "{{courseId}}",
                  "code": "{{courseCode.Value}}",
                  "name": {
                    "ko": "자료구조",
                    "en": "Data Structures"
                  },
                  "credits": 3.0
                }
              ],
              "offerings": [
                {
                  "offeringId": "{{offeringId}}",
                  "courseId": "{{courseId}}",
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
        InstitutionId institutionId,
        AcademicTerm term,
        byte[] catalogBytes)
    {
        string catalogId = createCatalogId(revision, institutionId, term);
        string revisionText = revision.Value.ToString(CultureInfo.InvariantCulture);
        string academicYearText = term.AcademicYear.Value.ToString(CultureInfo.InvariantCulture);
        string semesterText = term.Semester.Value.ToString(CultureInfo.InvariantCulture);
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
                    "id": "{{institutionId.Value}}",
                    "name": {
                      "ko": "한동대학교",
                      "en": "Handong Global University"
                    }
                  },
                  "term": {
                    "id": "{{term.Id}}",
                    "academicYear": {{academicYearText}},
                    "semester": {{semesterText}}
                  },
                  "revision": {{revisionText}},
                  "file": {
                    "relativePath": "{{institutionId.Value}}/{{term.Id}}/catalog-{{revision.FileComponent}}.json",
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
        CourseChoiceGroup[] courseChoiceGroups)
    {
        PlanId planId = PlanId.CreateNew();
        PlanningPlan plan = new PlanningPlan(
            planId,
            new PlanName("저장된 시간표"),
            createCatalogBinding(revision),
            new PlanningPlanContent(
                courseChoiceGroups,
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(
            plan.CatalogBinding,
            planId,
            new PlanningPlan[] { plan });
    }

    private static CourseChoiceGroup createCourseChoiceGroup(OfferingId offeringId)
    {
        return CourseChoiceGroup.CreateWithAcceptableOfferings(
            CourseChoiceGroupId.CreateNew(),
            new CourseId(COURSE_ID),
            new OfferingId[] { offeringId });
    }

    private static PlanCatalogBinding createCatalogBinding(CatalogRevision revision)
    {
        return CreateCatalogPackage(revision).CreatePlanCatalogBinding();
    }

    private static string createCatalogId(CatalogRevision revision)
    {
        return createCatalogId(revision, DEFAULT_INSTITUTION_ID, DEFAULT_TERM);
    }

    private static string createCatalogId(
        CatalogRevision revision,
        InstitutionId institutionId,
        AcademicTerm term)
    {
        return institutionId.Value + ":" + term.Id + ":" + revision.FileComponent;
    }
}
