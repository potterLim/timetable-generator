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
        StringAssert.Contains(json, "\"schemaVersion\": 2");
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
        Assert.HasCount(1, restoredWorkspace.Plans[0].ScheduledCourseChoices);
        Assert.HasCount(1, restoredWorkspace.Plans[0].UnscheduledOfferingSelections);
        Assert.HasCount(1, restoredWorkspace.Plans[0].PersonalSchedules);
        PersonalSchedule restoredPersonalSchedule =
            restoredWorkspace.Plans[0].PersonalSchedules[0];
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
            "\"schemaVersion\": 2,",
            "\"schemaVersion\": 2,\n  \"unexpected\": true,",
            StringComparison.Ordinal);
        string duplicatePropertyJson = validJson.Replace(
            "\"schemaVersion\": 2,",
            "\"schemaVersion\": 2,\n  \"schemaVersion\": 2,",
            StringComparison.Ordinal);
        string unsupportedSchemaJson = validJson.Replace(
            "\"schemaVersion\": 2,",
            "\"schemaVersion\": 3,",
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
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 2");
        StringAssert.Contains(migratedJson, "\"personalSchedules\": []");
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
                new ScheduledCourseChoice[]
                {
                    new ScheduledCourseChoice(
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
                Array.Empty<ScheduledCourseChoice>(),
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
        WeeklyTimeRange timeRange = new WeeklyTimeRange(
            EDay.Wednesday,
            new DailyTimeRange(
                new ScheduleTime(12, 20),
                new ScheduleTime(13, 20)));
        return new PersonalSchedule(
            new PersonalScheduleId(
                Guid.Parse("33333333-3333-3333-3333-333333333333")),
            new PersonalScheduleTitle("랩 미팅"),
            new WeeklyTimeRange[] { timeRange },
            details);
    }
}
