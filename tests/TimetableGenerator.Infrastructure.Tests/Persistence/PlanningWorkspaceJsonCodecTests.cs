using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
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
        Assert.AreEqual(new WorkspaceGeneration(7), restoredDocument.Generation);
        Assert.AreEqual(workspace.ActivePlanId, restoredWorkspace.ActivePlanId);
        Assert.HasCount(2, restoredWorkspace.Plans);
        Assert.HasCount(1, restoredWorkspace.Plans[0].ScheduledCourseChoices);
        Assert.HasCount(1, restoredWorkspace.Plans[0].UnscheduledOfferingSelections);
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
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"unexpected\": true,",
            StringComparison.Ordinal);
        string duplicatePropertyJson = validJson.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal);
        string unsupportedSchemaJson = validJson.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 2,",
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

        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(missingPlansJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(Encoding.UTF8.GetBytes(invalidPlanIdJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(
            () => codec.Deserialize(ReadOnlyMemory<byte>.Empty));
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
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1));
        PlanningPlan firstPlan = new PlanningPlan(
            new PlanId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new PlanName(firstPlanName),
            catalogBinding,
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
            });
        PlanningPlan secondPlan = new PlanningPlan(
            new PlanId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new PlanName("대안 시간표"),
            catalogBinding,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
        return new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
    }
}
