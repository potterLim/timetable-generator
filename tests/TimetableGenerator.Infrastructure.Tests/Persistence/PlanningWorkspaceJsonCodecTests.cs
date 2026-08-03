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
        PlanningWorkspaceDocument document = new PlanningWorkspaceDocument(new WorkspaceGeneration(7), workspace);

        byte[] firstContent = codec.Serialize(document);
        byte[] secondContent = codec.Serialize(document);
        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(firstContent);
        PlanningWorkspace restoredWorkspace = restoredDocument.Workspace;
        string json = Encoding.UTF8.GetString(firstContent);

        CollectionAssert.AreEqual(firstContent, secondContent);
        StringAssert.Contains(json, "기본 시간표");
        StringAssert.Contains(json, "\"schemaVersion\": 5");
        StringAssert.Contains(json, "\"catalog\": {");
        StringAssert.Contains(json, "\"courseChoiceGroups\"");
        StringAssert.Contains(json, "\"lastViewedRecommendation\"");
        StringAssert.Contains(json, "\"preference\": \"acceptable\"");
        StringAssert.Contains(json, "랩 미팅");
        StringAssert.Contains(json, "\"institutionId\": \"handong-global-university\"");
        StringAssert.Contains(json, "\"artifactSha256\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"");
        Assert.AreEqual(new WorkspaceGeneration(7), restoredDocument.Generation);
        Assert.AreEqual(workspace.ActivePlanIdOrNull, restoredWorkspace.ActivePlanIdOrNull);
        Assert.AreEqual(workspace.CatalogBinding, restoredWorkspace.CatalogBinding);
        Assert.HasCount(2, restoredWorkspace.Plans);
        Assert.HasCount(1, restoredWorkspace.Plans[0].CourseChoiceGroups);
        Assert.HasCount(1, restoredWorkspace.Plans[0].UnscheduledOfferingSelections);
        Assert.HasCount(1, restoredWorkspace.Plans[0].PersonalSchedules);
        ScheduleRecommendationBookmark? restoredBookmarkOrNull = restoredWorkspace.Plans[0].LastViewedRecommendationOrNull;
        Assert.IsNotNull(restoredBookmarkOrNull);
        Assert.AreEqual("handong-global-university:2026-2:CSE30001:02", restoredBookmarkOrNull.SelectedOfferingIds[0].Value);
        Assert.IsNull(restoredWorkspace.Plans[1].LastViewedRecommendationOrNull);
        PersonalSchedule restoredPersonalSchedule = restoredWorkspace.Plans[0].PersonalSchedules[0];
        Assert.HasCount(4, restoredPersonalSchedule.TimeRanges);
        Assert.AreEqual(EDay.Wednesday, restoredPersonalSchedule.TimeRanges[0].Day);
        Assert.AreEqual(EDay.Friday, restoredPersonalSchedule.TimeRanges[1].Day);
        Assert.AreEqual(EDay.Saturday, restoredPersonalSchedule.TimeRanges[2].Day);
        Assert.AreEqual(EDay.Sunday, restoredPersonalSchedule.TimeRanges[3].Day);
        Assert.AreEqual("A", restoredPersonalSchedule.Details.SectionOrNull?.Value);
        Assert.AreEqual("김교수", restoredPersonalSchedule.Details.InstructorOrNull?.Value);
        Assert.AreEqual("느헤미야홀", restoredPersonalSchedule.Details.LocationOrNull?.Value);
        Assert.AreEqual(new InstitutionId("handong-global-university"), restoredWorkspace.Plans[0].CatalogBinding.InstitutionId);
        Assert.AreEqual(new CatalogArtifactSha256(new string('a', 64)), restoredWorkspace.Plans[0].CatalogBinding.ArtifactSha256);
        Assert.AreEqual("handong-global-university:2026-2:CSE30002:01", restoredWorkspace.Plans[0].UnscheduledOfferingSelections[0].OfferingId.Value);
    }

    [TestMethod]
    public void CodecRoundTripsAnEmptyWorkspaceWithItsCatalogBinding()
    {
        PlanCatalogBinding catalogBinding = createCatalogBinding();
        PlanningWorkspace workspace = new PlanningWorkspace(catalogBinding, null, Array.Empty<PlanningPlan>());
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();

        byte[] content = codec.Serialize(new PlanningWorkspaceDocument(new WorkspaceGeneration(11), workspace));
        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(content);
        string json = Encoding.UTF8.GetString(content);

        StringAssert.Contains(json, "\"schemaVersion\": 5");
        StringAssert.Contains(json, "\"activePlanId\": null");
        StringAssert.Contains(json, "\"plans\": []");
        StringAssert.Contains(json, "\"catalogId\": \"handong-global-university:2026-2:r0001\"");
        Assert.AreEqual(new WorkspaceGeneration(11), restoredDocument.Generation);
        Assert.AreEqual(catalogBinding, restoredDocument.Workspace.CatalogBinding);
        Assert.IsNull(restoredDocument.Workspace.ActivePlanIdOrNull);
        Assert.IsFalse(restoredDocument.Workspace.HasPlans);
        Assert.IsEmpty(restoredDocument.Workspace.Plans);
    }

    [TestMethod]
    public void CodecRejectsUnknownDuplicateAndUnsupportedSchemaProperties()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(createContent(codec, "기본 시간표"));
        string unknownPropertyJson = validJson.Replace("\"schemaVersion\": 5,", "\"schemaVersion\": 5,\n  \"unexpected\": true,", StringComparison.Ordinal);
        string duplicatePropertyJson = validJson.Replace("\"schemaVersion\": 5,", "\"schemaVersion\": 5,\n  \"schemaVersion\": 5,", StringComparison.Ordinal);
        string unsupportedSchemaJson = validJson.Replace("\"schemaVersion\": 5,", "\"schemaVersion\": 6,", StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(unknownPropertyJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(duplicatePropertyJson)));
        Assert.ThrowsExactly<UnsupportedWorkspaceSchemaVersionException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(unsupportedSchemaJson)));
    }

    [TestMethod]
    public void CodecRejectsMissingAndInvalidProductValues()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(createContent(codec, "기본 시간표"));
        string missingPlansJson = validJson.Replace("  \"plans\": [", "  \"removedPlans\": [", StringComparison.Ordinal);
        string invalidPlanIdJson = validJson.Replace("11111111-1111-1111-1111-111111111111", "not-a-guid", StringComparison.Ordinal);
        string missingArtifactSha256Json = validJson.Replace("\"artifactSha256\"", "\"removedArtifactSha256\"", StringComparison.Ordinal);
        string invalidArtifactSha256Json = validJson.Replace(new string('a', 64), "not-a-sha256", StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(missingPlansJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(invalidPlanIdJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(missingArtifactSha256Json)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(invalidArtifactSha256Json)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(ReadOnlyMemory<byte>.Empty));
    }

    [TestMethod]
    public void CodecRejectsCurrentDocumentsWithInconsistentWorkspaceState()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string populatedJson = Encoding.UTF8.GetString(createContent(codec, "기본 시간표"));
        string missingActivePlanJson = replaceFirst(populatedJson, "\"activePlanId\": \"11111111-1111-1111-1111-111111111111\"", "\"activePlanId\": null");
        string mismatchedCatalogBindingJson = replaceFirst(populatedJson, new string('a', 64), new string('b', 64));

        PlanCatalogBinding catalogBinding = createCatalogBinding();
        PlanningWorkspace emptyWorkspace = new PlanningWorkspace(catalogBinding, null, Array.Empty<PlanningPlan>());
        string emptyJson = Encoding.UTF8.GetString(codec.Serialize(new PlanningWorkspaceDocument(new WorkspaceGeneration(1), emptyWorkspace)));
        string unexpectedActivePlanJson = emptyJson.Replace("\"activePlanId\": null", "\"activePlanId\": \"11111111-1111-1111-1111-111111111111\"", StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(missingActivePlanJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(mismatchedCatalogBindingJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(unexpectedActivePlanJson)));
    }

    [TestMethod]
    public void CodecRejectsPersonalSchedulesOutsideSupportedTimePolicy()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(createContent(codec, "기본 시간표"));
        string impreciseTimeJson = validJson.Replace("\"start\": \"12:20\"", "\"start\": \"12:21\"", StringComparison.Ordinal);
        string tooShortDurationJson = validJson.Replace("\"end\": \"13:20\"", "\"end\": \"12:30\"", StringComparison.Ordinal);

        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(impreciseTimeJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(tooShortDurationJson)));
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
                  "name": "나의 시간표",
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

        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(Encoding.UTF8.GetBytes(LEGACY_JSON));
        byte[] migratedContent = codec.Serialize(restoredDocument);
        string migratedJson = Encoding.UTF8.GetString(migratedContent);

        Assert.AreEqual(new WorkspaceGeneration(4), restoredDocument.Generation);
        Assert.IsEmpty(restoredDocument.Workspace.Plans[0].PersonalSchedules);
        Assert.AreEqual("2026-2학기 시간표", restoredDocument.Workspace.Plans[0].Name.Value);
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 5");
        StringAssert.Contains(migratedJson, "\"catalog\": {");
        StringAssert.Contains(migratedJson, "\"courseChoiceGroups\": []");
        StringAssert.Contains(migratedJson, "\"personalSchedules\": []");
        StringAssert.Contains(migratedJson, "\"lastViewedRecommendation\": null");
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

        PlanningWorkspaceDocument firstDocument = codec.Deserialize(Encoding.UTF8.GetBytes(VERSION_TWO_JSON));
        PlanningWorkspaceDocument secondDocument = codec.Deserialize(Encoding.UTF8.GetBytes(VERSION_TWO_JSON));
        CourseChoiceGroup firstGroup = firstDocument.Workspace.Plans[0].CourseChoiceGroups[0];
        CourseChoiceGroup secondGroup = secondDocument.Workspace.Plans[0].CourseChoiceGroups[0];
        string migratedJson = Encoding.UTF8.GetString(codec.Serialize(firstDocument));

        Assert.AreEqual(firstGroup.Id, secondGroup.Id);
        Assert.HasCount(1, firstGroup.CourseCandidates);
        Assert.HasCount(2, firstGroup.CourseCandidates[0].OfferingCandidates);
        Assert.AreEqual(EOfferingPreference.Acceptable, firstGroup.CourseCandidates[0].OfferingCandidates[0].Preference);
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 5");
        StringAssert.Contains(migratedJson, "\"courseChoiceGroups\"");
    }

    [TestMethod]
    public void CodecReadsVersionThreePlansWithoutRecommendationBookmarks()
    {
        const string VERSION_THREE_JSON = """
            {
              "schemaVersion": 3,
              "generation": 8,
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
                  "courseChoiceGroups": [
                    {
                      "id": "33333333-3333-3333-3333-333333333333",
                      "cardinality": "exactlyOne",
                      "courseCandidates": [
                        {
                          "courseId": "handong-global-university:CSE30001",
                          "offeringCandidates": [
                            {
                              "offeringId": "handong-global-university:2026-2:CSE30001:01",
                              "preference": "acceptable"
                            }
                          ]
                        }
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

        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(Encoding.UTF8.GetBytes(VERSION_THREE_JSON));
        string migratedJson = Encoding.UTF8.GetString(codec.Serialize(restoredDocument));

        Assert.IsNull(restoredDocument.Workspace.Plans[0].LastViewedRecommendationOrNull);
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 5");
        StringAssert.Contains(migratedJson, "\"lastViewedRecommendation\": null");
    }

    [TestMethod]
    public void CodecMigratesVersionFourWithoutLosingRecommendationBookmarks()
    {
        const string VERSION_FOUR_JSON = """
            {
              "schemaVersion": 4,
              "generation": 9,
              "activePlanId": "11111111-1111-1111-1111-111111111111",
              "plans": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "나의 시간표",
                  "catalog": {
                    "catalogId": "handong-global-university:2026-2:r0001",
                    "institutionId": "handong-global-university",
                    "term": "2026-2",
                    "revision": 1,
                    "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                  },
                  "courseChoiceGroups": [
                    {
                      "id": "33333333-3333-3333-3333-333333333333",
                      "cardinality": "exactlyOne",
                      "courseCandidates": [
                        {
                          "courseId": "handong-global-university:CSE30001",
                          "offeringCandidates": [
                            {
                              "offeringId": "handong-global-university:2026-2:CSE30001:02",
                              "preference": "acceptable"
                            }
                          ]
                        }
                      ]
                    }
                  ],
                  "unscheduledSelections": [],
                  "personalSchedules": [],
                  "lastViewedRecommendation": {
                    "scheduledOfferingIds": [
                      "handong-global-university:2026-2:CSE30001:02"
                    ]
                  }
                },
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "name": "2026-2학기 시간표 2",
                  "catalog": {
                    "catalogId": "handong-global-university:2026-2:r0001",
                    "institutionId": "handong-global-university",
                    "term": "2026-2",
                    "revision": 1,
                    "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                  },
                  "courseChoiceGroups": [],
                  "unscheduledSelections": [],
                  "personalSchedules": [],
                  "lastViewedRecommendation": null
                }
              ]
            }
            """;
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();

        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(Encoding.UTF8.GetBytes(VERSION_FOUR_JSON));
        PlanningWorkspace restoredWorkspace = restoredDocument.Workspace;
        ScheduleRecommendationBookmark? restoredBookmarkOrNull = restoredWorkspace.Plans[0].LastViewedRecommendationOrNull;
        string migratedJson = Encoding.UTF8.GetString(codec.Serialize(restoredDocument));

        Assert.IsNotNull(restoredBookmarkOrNull);
        Assert.AreEqual("handong-global-university:2026-2:CSE30001:02", restoredBookmarkOrNull.ScheduledOfferingIds[0].Value);
        Assert.AreEqual(restoredWorkspace.Plans[0].CatalogBinding, restoredWorkspace.CatalogBinding);
        Assert.AreEqual("2026-2학기 시간표", restoredWorkspace.Plans[0].Name.Value);
        Assert.AreEqual("2026-2학기 시간표 (2)", restoredWorkspace.Plans[1].Name.Value);
        StringAssert.Contains(migratedJson, "\"schemaVersion\": 5");
        StringAssert.Contains(migratedJson, "\"catalog\": {");
        StringAssert.Contains(migratedJson, "handong-global-university:2026-2:CSE30001:02");
    }

    [TestMethod]
    public void CodecPreservesUserNamesWhenLegacyMigrationWouldCollideOrIsComplete()
    {
        const string LEGACY_COLLISION_JSON = """
            {
              "schemaVersion": 1,
              "generation": 4,
              "activePlanId": "11111111-1111-1111-1111-111111111111",
              "plans": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "나의 시간표",
                  "catalog": {
                    "catalogId": "handong-global-university:2026-2:r0001",
                    "institutionId": "handong-global-university",
                    "term": "2026-2",
                    "revision": 1,
                    "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                  },
                  "scheduledChoices": [],
                  "unscheduledSelections": []
                },
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "name": "2026-2학기 시간표",
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

        PlanningWorkspaceDocument legacyDocument = codec.Deserialize(Encoding.UTF8.GetBytes(LEGACY_COLLISION_JSON));
        PlanningWorkspaceDocument currentDocument = codec.Deserialize(createContent(codec, "나의 시간표"));

        Assert.AreEqual("나의 시간표", legacyDocument.Workspace.Plans[0].Name.Value);
        Assert.AreEqual("2026-2학기 시간표", legacyDocument.Workspace.Plans[1].Name.Value);
        Assert.AreEqual("나의 시간표", currentDocument.Workspace.Plans[0].Name.Value);
    }

    [TestMethod]
    public void CodecRejectsMalformedRecommendationBookmarks()
    {
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();
        string validJson = Encoding.UTF8.GetString(createContent(codec, "기본 시간표"));
        const string BOOKMARK_OFFERING_ID = "handong-global-university:2026-2:CSE30001:02";
        string bookmarkOfferingIdLiteral = "\"" + BOOKMARK_OFFERING_ID + "\"";
        int bookmarkOfferingIdIndex = validJson.LastIndexOf(bookmarkOfferingIdLiteral, StringComparison.Ordinal);
        string duplicateOfferingJson = validJson.Insert(bookmarkOfferingIdIndex + bookmarkOfferingIdLiteral.Length, ",\n        " + bookmarkOfferingIdLiteral);
        int bookmarkPropertyIndex = validJson.LastIndexOf("\"scheduledOfferingIds\"", StringComparison.Ordinal);
        int bookmarkArrayStartIndex = validJson.IndexOf('[', bookmarkPropertyIndex);
        int bookmarkArrayEndIndex = validJson.IndexOf(']', bookmarkArrayStartIndex);
        string emptyBookmarkJson = validJson.Remove(bookmarkArrayStartIndex + 1, bookmarkArrayEndIndex - bookmarkArrayStartIndex - 1);
        int bookmarkObjectEndIndex = validJson.IndexOf('}', bookmarkArrayEndIndex);
        string unknownBookmarkPropertyJson = validJson.Insert(bookmarkObjectEndIndex, ",\n      \"unexpected\": true\n    ");

        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(duplicateOfferingJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(emptyBookmarkJson)));
        Assert.ThrowsExactly<WorkspaceDocumentException>(() => codec.Deserialize(Encoding.UTF8.GetBytes(unknownBookmarkPropertyJson)));
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
        CourseChoiceGroupId groupId = new CourseChoiceGroupId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        CourseChoiceGroup group = new CourseChoiceGroup(groupId, ECourseChoiceCardinality.ExactlyOne, new CourseCandidate[] { firstCourse, secondCourse });
        PlanCatalogBinding binding = new PlanCatalogBinding(
            new CatalogId("institution:2026-2:r0001"),
            new InstitutionId("institution"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('b', 64)));
        PlanningPlan plan = new PlanningPlan(new PlanId(Guid.Parse("55555555-5555-5555-5555-555555555555")), new PlanName("선택 계획"), binding, new PlanningPlanContent(new CourseChoiceGroup[] { group }, Array.Empty<UnscheduledOfferingSelection>(), Array.Empty<PersonalSchedule>()));
        PlanningWorkspace workspace = new PlanningWorkspace(binding, plan.Id, new PlanningPlan[] { plan });
        PlanningWorkspaceJsonCodec codec = new PlanningWorkspaceJsonCodec();

        byte[] content = codec.Serialize(new PlanningWorkspaceDocument(new WorkspaceGeneration(1), workspace));
        PlanningWorkspaceDocument restoredDocument = codec.Deserialize(content);
        CourseChoiceGroup restoredGroup = restoredDocument.Workspace.Plans[0].CourseChoiceGroups[0];

        Assert.AreEqual(groupId, restoredGroup.Id);
        Assert.HasCount(2, restoredGroup.CourseCandidates);
        Assert.AreEqual(EOfferingPreference.Preferred, restoredGroup.CourseCandidates[0].OfferingCandidates[0].Preference);
        Assert.AreEqual(EOfferingPreference.Excluded, restoredGroup.CourseCandidates[0].OfferingCandidates[1].Preference);
        Assert.AreEqual(EOfferingPreference.Acceptable, restoredGroup.CourseCandidates[1].OfferingCandidates[0].Preference);
        Assert.IsTrue(restoredGroup.CourseCandidates[0].OfferingCandidates[0].IsEligible);
        Assert.IsFalse(restoredGroup.CourseCandidates[0].OfferingCandidates[1].IsEligible);
    }

    private static byte[] createContent(PlanningWorkspaceJsonCodec codec, string planName)
    {
        PlanningWorkspaceDocument document = new PlanningWorkspaceDocument(new WorkspaceGeneration(1), createWorkspace(planName));
        return codec.Serialize(document);
    }

    private static PlanningWorkspace createWorkspace(string firstPlanName)
    {
        PlanCatalogBinding catalogBinding = createCatalogBinding();
        OfferingId lastViewedOfferingId = new OfferingId("handong-global-university:2026-2:CSE30001:02");
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
                            lastViewedOfferingId,
                        }),
                },
                new UnscheduledOfferingSelection[]
                {
                    new UnscheduledOfferingSelection(
                        new CourseId("handong-global-university:CSE30002"),
                        new OfferingId(
                            "handong-global-university:2026-2:CSE30002:01")),
                },
                new PersonalSchedule[] { createPersonalSchedule() }),
            new ScheduleRecommendationBookmark(new OfferingId[] { lastViewedOfferingId }));
        PlanningPlan secondPlan = new PlanningPlan(new PlanId(Guid.Parse("22222222-2222-2222-2222-222222222222")), new PlanName("대안 시간표"), catalogBinding, new PlanningPlanContent(Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>(), Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(catalogBinding, firstPlan.Id, new PlanningPlan[] { firstPlan, secondPlan });
    }

    private static PlanCatalogBinding createCatalogBinding()
    {
        return new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
    }

    private static string replaceFirst(string source, string oldValue, string newValue)
    {
        int valueIndex = source.IndexOf(oldValue, StringComparison.Ordinal);
        if (valueIndex < 0)
        {
            throw new InvalidOperationException("The test JSON does not contain the expected value.");
        }

        return source.Remove(valueIndex, oldValue.Length).Insert(valueIndex, newValue);
    }

    private static PersonalSchedule createPersonalSchedule()
    {
        PersonalScheduleDetails details = new PersonalScheduleDetails(new PersonalScheduleSection("A"), new PersonalScheduleInstructor("김교수"), new PersonalScheduleLocation("느헤미야홀"));
        DailyTimeRange sharedTimeRange = new DailyTimeRange(new ScheduleTime(12, 20), new ScheduleTime(13, 20));
        WeeklyTimeRange wednesdayTimeRange = new WeeklyTimeRange(EDay.Wednesday, sharedTimeRange);
        WeeklyTimeRange fridayTimeRange = new WeeklyTimeRange(EDay.Friday, sharedTimeRange);
        WeeklyTimeRange saturdayTimeRange = new WeeklyTimeRange(EDay.Saturday, sharedTimeRange);
        WeeklyTimeRange sundayTimeRange = new WeeklyTimeRange(EDay.Sunday, sharedTimeRange);
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
