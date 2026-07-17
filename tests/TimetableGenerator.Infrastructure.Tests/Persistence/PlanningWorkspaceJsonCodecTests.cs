using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using TimetableGenerator.Infrastructure.Persistence;

namespace TimetableGenerator.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class PlanningWorkspaceJsonCodecTests
{
    [TestMethod]
    public void CodecRoundTripsAllPlanStatesDeterministically()
    {
        PlanningWorkspace workspace = createWorkspace("기본 시간표");
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        PlanningWorkspaceDocument document = new PlanningWorkspaceDocument(
            new WorkspaceGeneration(7),
            workspace);

        byte[] firstContent = codec.Serialize(document);
        byte[] secondContent = codec.Serialize(document);
        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(firstContent);
        PlanningWorkspace restoredWorkspace = restoredDocument.Workspace;
        string json = Encoding.UTF8.GetString(firstContent);

        CollectionAssert.AreEqual(firstContent, secondContent);
        StringAssert.Contains(json, "기본 시간표");
        StringAssert.Contains(json, "\"schemaVersion\": 3");
        StringAssert.Contains(json, "\"courseChoiceGroups\"");
        StringAssert.Contains(json, "\"preference\": \"acceptable\"");
        StringAssert.Contains(json, "랩 미팅");
        StringAssert.Contains(
            json,
            "\"institutionId\": \"handong-global-university\"");
        StringAssert.Contains(
            json,
            "\"artifactSha256\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"");
        Assert.AreEqual(new WorkspaceGeneration(7), restoredDocument.Generation);
        Assert.AreEqual(workspace.ActivePlanId, restoredWorkspace.ActivePlanId);
        Assert.HasCount(2, restoredWorkspace.Plans);
        Assert.HasCount(1, restoredWorkspace.Plans[0].CourseChoiceGroups);
        Assert.HasCount(1, restoredWorkspace.Plans[0].UnscheduledOfferingSelections);
        Assert.HasCount(1, restoredWorkspace.Plans[0].PersonalSchedules);
        PersonalSchedule restoredPersonalSchedule =
            restoredWorkspace.Plans[0].PersonalSchedules[0];
        Assert.HasCount(4, restoredPersonalSchedule.TimeRanges);
        Assert.AreEqual(EDay.Wednesday, restoredPersonalSchedule.TimeRanges[0].Day);
        Assert.AreEqual(EDay.Friday, restoredPersonalSchedule.TimeRanges[1].Day);
        Assert.AreEqual(EDay.Saturday, restoredPersonalSchedule.TimeRanges[2].Day);
        Assert.AreEqual(EDay.Sunday, restoredPersonalSchedule.TimeRanges[3].Day);
        Assert.AreEqual("A", restoredPersonalSchedule.Details.SectionOrNull?.Value);
        Assert.AreEqual("김교수", restoredPersonalSchedule.Details.InstructorOrNull?.Value);
        Assert.AreEqual("느헤미야홀", restoredPersonalSchedule.Details.LocationOrNull?.Value);
        Assert.AreEqual(
            new InstitutionId("handong-global-university"),
            restoredWorkspace.Plans[0].CatalogBinding.InstitutionId);
        Assert.AreEqual(
            new CatalogArtifactSha256(new string('a', 64)),
            restoredWorkspace.Plans[0].CatalogBinding.ArtifactSha256);
        Assert.AreEqual(
            "handong-global-university:2026-2:CSE30002:01",
            restoredWorkspace.Plans[0]
                .UnscheduledOfferingSelections[0]
                .OfferingId
                .Value);
    }

    [TestMethod]
    public void CodecRejectsUnknownDuplicateAndUnsupportedSchemaProperties()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(
            createContent(codec, "기본 시간표"));
        string unknownPropertyJson = validJson.Replace(
            "\"schemaVersion\": 3,",
            "\"schemaVersion\": 3,\n  \"unexpected\": true,",
            StringComparison.Ordinal);
        string duplicatePropertyJson = validJson.Replace(
            "\"schemaVersion\": 3,",
            "\"schemaVersion\": 3,\n  \"schemaVersion\": 3,",
            StringComparison.Ordinal);
        string unsupportedSchemaJson = validJson.Replace(
            "\"schemaVersion\": 3,",
            "\"schemaVersion\": 4,",
            StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(unknownPropertyJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(duplicatePropertyJson)));
        Assert.ThrowsExactly<UnsupportedWorkspaceSchemaVersionException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(unsupportedSchemaJson)));
    }

    [TestMethod]
    public void CodecRejectsMissingAndInvalidProductValues()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(
            createContent(codec, "기본 시간표"));
        string missingPlansJson = validJson.Replace(
            "  \"plans\": [",
            "  \"removedPlans\": [",
            StringComparison.Ordinal);
        string invalidPlanIdJson = validJson.Replace(
            "11111111-1111-1111-1111-111111111111",
            "not-a-guid",
            StringComparison.Ordinal);
        string missingArtifactSha256Json = validJson.Replace(
            "\"artifactSha256\"",
            "\"removedArtifactSha256\"",
            StringComparison.Ordinal);
        string invalidArtifactSha256Json = validJson.Replace(
            new string('a', 64),
            "not-a-sha256",
            StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(missingPlansJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(invalidPlanIdJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(
                Encoding.UTF8.GetBytes(missingArtifactSha256Json)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(
                Encoding.UTF8.GetBytes(invalidArtifactSha256Json)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(ReadOnlyMemory<byte>.Empty));
    }

    [TestMethod]
    public void CodecRejectsPersonalSchedulesOutsideSupportedTimePolicy()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(
            createContent(codec, "기본 시간표"));
        string impreciseTimeJson = validJson.Replace(
            "\"start\": \"12:20\"",
            "\"start\": \"12:21\"",
            StringComparison.Ordinal);
        string tooShortDurationJson = validJson.Replace(
            "\"end\": \"13:20\"",
            "\"end\": \"12:30\"",
            StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(impreciseTimeJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(
                Encoding.UTF8.GetBytes(tooShortDurationJson)));
    }

    [TestMethod]
    public void CodecReadsLegacyVersionOnePlansWithEmptyPersonalSchedules()
    {
        const string LEGACY_JSON = """
            {
              "schemaVersion": 1,
              "generation": 4,
              "activePlanId": "11111111-1111-1111-1111-111111111111",
              "plans": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "기존 계획",
                  "catalog": {
                    "catalogId": "handong-global-university:2026-2:r0001",
                    "institutionId": "handong-global-university",
                    "term": "2026-2",
                    "revision": 1,
                    "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                  },
                  "scheduledChoices": [],
                  "unscheduledSelections": []
                }
              ]
            }
            """;
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();

        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(
            Encoding.UTF8.GetBytes(LEGACY_JSON));
        byte[] migratedContent = codec.Serialize(restoredDocument);
        string migratedJson = Encoding.UTF8.GetString(migratedContent);

        Assert.AreEqual(new WorkspaceGeneration(4), restoredDocument.Generation);
        Assert.IsEmpty(restoredDocument.Workspace.Plans[0].PersonalSchedules);
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 3");
        StringAssert.Contains(migratedJson, "\"courseChoiceGroups\": []");
        StringAssert.Contains(migratedJson, "\"personalSchedules\": []");
    }

    [TestMethod]
    public void CodecMigratesVersionTwoChoicesToStableAcceptableGroups()
    {
        const string VERSION_TWO_JSON = """
            {
              "schemaVersion": 2,
              "generation": 4,
              "activePlanId": "11111111-1111-1111-1111-111111111111",
              "plans": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "기존 계획",
                  "catalog": {
                    "catalogId": "handong-global-university:2026-2:r0001",
                    "institutionId": "handong-global-university",
                    "term": "2026-2",
                    "revision": 1,
                    "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                  },
                  "scheduledChoices": [
                    {
                      "courseId": "handong-global-university:CSE30001",
                      "offeringIds": [
                        "handong-global-university:2026-2:CSE30001:01",
                        "handong-global-university:2026-2:CSE30001:02"
                      ]
                    }
                  ],
                  "unscheduledSelections": [],
                  "personalSchedules": []
                }
              ]
            }
            """;
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();

        PlanningWorkspaceDocument firstDocument = codec.Deserialize(
            Encoding.UTF8.GetBytes(VERSION_TWO_JSON));
        PlanningWorkspaceDocument secondDocument = codec.Deserialize(
            Encoding.UTF8.GetBytes(VERSION_TWO_JSON));
        CourseChoiceGroup firstGroup =
            firstDocument.Workspace.Plans[0].CourseChoiceGroups[0];
        CourseChoiceGroup secondGroup =
            secondDocument.Workspace.Plans[0].CourseChoiceGroups[0];
        string migratedJson = Encoding.UTF8.GetString(
            codec.Serialize(firstDocument));

        Assert.AreEqual(firstGroup.Id, secondGroup.Id);
        Assert.HasCount(1, firstGroup.CourseCandidates);
        Assert.HasCount(2, firstGroup.CourseCandidates[0].OfferingCandidates);
        Assert.AreEqual(
            EOfferingPreference.Acceptable,
            firstGroup.CourseCandidates[0].OfferingCandidates[0].Preference);
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 3");
        StringAssert.Contains(migratedJson, "\"courseChoiceGroups\"");
    }

    [TestMethod]
    public void CodecRoundTripsCrossCoursePreferences()
    {
        CourseCandidate firstCourse = new CourseCandidate(
            new CourseId("institution:AAA10001"),
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    new OfferingId("institution:term:AAA10001:01"),
                    EOfferingPreference.Preferred),
                new OfferingCandidate(
                    new OfferingId("institution:term:AAA10001:02"),
                    EOfferingPreference.Excluded),
            });
        CourseCandidate secondCourse = new CourseCandidate(
            new CourseId("institution:BBB10001"),
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    new OfferingId("institution:term:BBB10001:01"),
                    EOfferingPreference.Acceptable),
            });
        CourseChoiceGroupId groupId = new CourseChoiceGroupId(
            Guid.Parse("44444444-4444-4444-4444-444444444444"));
        CourseChoiceGroup group = new CourseChoiceGroup(
            groupId,
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { firstCourse, secondCourse });
        PlanCatalogBinding binding = new PlanCatalogBinding(
            new CatalogId("institution:2026-2:r0001"),
            new InstitutionId("institution"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('b', 64)));
        PlanningPlan plan = new PlanningPlan(
            new PlanId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new PlanName("선택 계획"),
            binding,
            new PlanningPlanContent(
                new CourseChoiceGroup[] { group },
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();

        byte[] content = codec.Serialize(new PlanningWorkspaceDocument(
            new WorkspaceGeneration(1),
            workspace));
        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(content);
        CourseChoiceGroup restoredGroup =
            restoredDocument.Workspace.Plans[0].CourseChoiceGroups[0];

        Assert.AreEqual(groupId, restoredGroup.Id);
        Assert.HasCount(2, restoredGroup.CourseCandidates);
        Assert.AreEqual(
            EOfferingPreference.Preferred,
            restoredGroup.CourseCandidates[0].OfferingCandidates[0].Preference);
        Assert.AreEqual(
            EOfferingPreference.Excluded,
            restoredGroup.CourseCandidates[0].OfferingCandidates[1].Preference);
        Assert.AreEqual(
            EOfferingPreference.Acceptable,
            restoredGroup.CourseCandidates[1].OfferingCandidates[0].Preference);
        Assert.IsTrue(
            restoredGroup.CourseCandidates[0].OfferingCandidates[0].IsEligible);
        Assert.IsFalse(
            restoredGroup.CourseCandidates[0].OfferingCandidates[1].IsEligible);
    }

    private static byte[] createContent(
        PlanningWorkspaceJsonCodec codec,
        string planName)
    {
        PlanningWorkspaceDocument document = new PlanningWorkspaceDocument(
            new WorkspaceGeneration(1),
            createWorkspace(planName));
        return codec.Serialize(document);
    }

    private static PlanningWorkspace createWorkspace(string firstPlanName)
    {
        PlanCatalogBinding catalogBinding = new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningPlan firstPlan = new PlanningPlan(
            new PlanId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new PlanName(firstPlanName),
            catalogBinding,
            new PlanningPlanContent(
                new CourseChoiceGroup[]
                {
                    CourseChoiceGroup.CreateWithAcceptableOfferings(
                        new CourseChoiceGroupId(Guid.Parse(
                            "33333333-3333-3333-3333-333333333333")),
                        new CourseId("handong-global-university:CSE30001"),
                        new OfferingId[]
                        {
                            new OfferingId(
                                "handong-global-university:2026-2:CSE30001:01"),
                            new OfferingId(
                                "handong-global-university:2026-2:CSE30001:02"),
                        }),
                },
                new UnscheduledOfferingSelection[]
                {
                    new UnscheduledOfferingSelection(
                        new CourseId("handong-global-university:CSE30002"),
                        new OfferingId(
                            "handong-global-university:2026-2:CSE30002:01")),
                },
                new PersonalSchedule[] { createPersonalSchedule() }));
        PlanningPlan secondPlan = new PlanningPlan(
            new PlanId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new PlanName("대안 시간표"),
            catalogBinding,
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
    }

    private static PersonalSchedule createPersonalSchedule()
    {
        PersonalScheduleDetails details = new PersonalScheduleDetails(
            new PersonalScheduleSection("A"),
            new PersonalScheduleInstructor("김교수"),
            new PersonalScheduleLocation("느헤미야홀"));
        DailyTimeRange sharedTimeRange = new DailyTimeRange(
            new ScheduleTime(12, 20),
            new ScheduleTime(13, 20));
        WeeklyTimeRange wednesdayTimeRange = new WeeklyTimeRange(
            EDay.Wednesday,
            sharedTimeRange);
        WeeklyTimeRange fridayTimeRange = new WeeklyTimeRange(
            EDay.Friday,
            sharedTimeRange);
        WeeklyTimeRange saturdayTimeRange = new WeeklyTimeRange(
            EDay.Saturday,
            sharedTimeRange);
        WeeklyTimeRange sundayTimeRange = new WeeklyTimeRange(
            EDay.Sunday,
            sharedTimeRange);
        return new PersonalSchedule(
            new PersonalScheduleId(
                Guid.Parse("33333333-3333-3333-3333-333333333333")),
            new PersonalScheduleTitle("랩 미팅"),
            new WeeklyTimeRange[]
            {
                wednesdayTimeRange,
                fridayTimeRange,
                saturdayTimeRange,
                sundayTimeRange,
            },
            details);
    }
}
