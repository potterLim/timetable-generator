using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Scheduling;

[TestClass]
public sealed class ScheduleRecommendationGeneratorOracleTests
{
    private const int FIXED_SEED = 20260718;

    private const int MAXIMUM_RECOMMENDATION_COUNT = 1_024;

    [TestMethod]
    public void GenerateRecommendationsMatchesDeterministicBruteForceOracle()
    {
        OracleFixture fixture = createFixture();
        IReadOnlyList<OracleSolution> expectedSolutions =
            enumerateFeasibleSolutions(fixture.Groups);
        ScheduleRecommendationResult firstResult = generate(fixture);
        ScheduleRecommendationResult secondResult = generate(fixture);

        Assert.AreEqual(
            EScheduleRecommendationCompletion.Completed,
            firstResult.Completion);
        Assert.HasCount(expectedSolutions.Count, firstResult.Recommendations);
        assertMatchesOracle(firstResult, expectedSolutions, fixture.Groups.Count);
        CollectionAssert.AreEqual(
            serializeRecommendations(firstResult.Recommendations),
            serializeRecommendations(secondResult.Recommendations),
            "Identical fixed-seed input must preserve recommendation order and scores.");
        Assert.IsGreaterThan(
            expectedSolutions.Count,
            getCartesianCombinationCount(fixture.Groups),
            "The fixture must exercise conflict pruning.");
        Assert.IsTrue(
            containsDifferentScores(expectedSolutions),
            "The fixture must exercise preference-score ordering.");
    }

    private static void assertMatchesOracle(
        ScheduleRecommendationResult actualResult,
        IReadOnlyList<OracleSolution> expectedSolutions,
        int expectedOfferingCount)
    {
        Dictionary<string, int> expectedScoresByKey =
            createExpectedScoresByKey(expectedSolutions);
        HashSet<string> actualKeys = new HashSet<string>(StringComparer.Ordinal);
        int previousScore = -1;

        foreach (ScheduleRecommendation recommendation
            in actualResult.Recommendations)
        {
            string key = createOfferingKey(recommendation.ScheduledOfferings);
            int expectedScore;
            bool isExpected = expectedScoresByKey.TryGetValue(
                key,
                out expectedScore);

            Assert.IsTrue(
                isExpected,
                "Generator returned a combination rejected by the oracle: " + key);
            Assert.IsTrue(
                actualKeys.Add(key),
                "Generator returned a duplicate combination: " + key);
            Assert.AreEqual(expectedScore, recommendation.Score.Value, key);
            Assert.IsGreaterThanOrEqualTo(
                previousScore,
                recommendation.Score.Value,
                "Recommendations must be ordered by ascending score.");
            Assert.HasCount(
                expectedOfferingCount,
                recommendation.ScheduledOfferings,
                "Exactly one offering must be chosen from every group.");
            assertConflictFree(recommendation.ScheduledOfferings);
            previousScore = recommendation.Score.Value;
        }

        foreach (OracleSolution expectedSolution in expectedSolutions)
        {
            Assert.Contains(
                expectedSolution.Key,
                actualKeys,
                "Generator omitted a feasible combination: " + expectedSolution.Key);
        }
    }

    private static void assertConflictFree(
        IReadOnlyList<ScheduledOffering> offerings)
    {
        for (int firstIndex = 0; firstIndex < offerings.Count; ++firstIndex)
        {
            for (int secondIndex = firstIndex + 1;
                secondIndex < offerings.Count;
                ++secondIndex)
            {
                Assert.IsFalse(
                    ScheduleConflictDetector.HasConflict(
                        offerings[firstIndex],
                        offerings[secondIndex]),
                    "The generator returned overlapping offerings.");
            }
        }
    }

    private static IReadOnlyList<OracleSolution> enumerateFeasibleSolutions(
        IReadOnlyList<OracleGroup> groups)
    {
        List<OracleSolution> solutions = new List<OracleSolution>();
        enumerateGroup(
            groups,
            0,
            new List<OracleCandidate>(),
            0,
            solutions);
        return solutions.AsReadOnly();
    }

    private static void enumerateGroup(
        IReadOnlyList<OracleGroup> groups,
        int groupIndex,
        List<OracleCandidate> selectedCandidates,
        int score,
        ICollection<OracleSolution> solutions)
    {
        if (groupIndex >= groups.Count)
        {
            List<ScheduledOffering> offerings = new List<ScheduledOffering>();
            foreach (OracleCandidate selectedCandidate in selectedCandidates)
            {
                offerings.Add(selectedCandidate.Offering);
            }

            solutions.Add(new OracleSolution(createOfferingKey(offerings), score));
            return;
        }

        foreach (OracleCandidate candidate in groups[groupIndex].Candidates)
        {
            if (hasConflict(candidate, selectedCandidates))
            {
                continue;
            }

            selectedCandidates.Add(candidate);
            enumerateGroup(
                groups,
                groupIndex + 1,
                selectedCandidates,
                checked(score + getPreferenceScore(candidate.Preference)),
                solutions);
            selectedCandidates.RemoveAt(selectedCandidates.Count - 1);
        }
    }

    private static bool hasConflict(
        OracleCandidate candidate,
        IEnumerable<OracleCandidate> selectedCandidates)
    {
        foreach (OracleCandidate selectedCandidate in selectedCandidates)
        {
            if (ScheduleConflictDetector.HasConflict(
                selectedCandidate.Offering,
                candidate.Offering))
            {
                return true;
            }
        }

        return false;
    }

    private static int getPreferenceScore(EOfferingPreference preference)
    {
        if (preference == EOfferingPreference.Preferred)
        {
            return 0;
        }

        if (preference == EOfferingPreference.Acceptable)
        {
            return 1;
        }

        throw new ArgumentOutOfRangeException(nameof(preference));
    }

    private static OracleFixture createFixture()
    {
        Random random = new Random(FIXED_SEED);
        List<CatalogCourse> courses = new List<CatalogCourse>();
        List<CatalogOffering> offerings = new List<CatalogOffering>();
        List<CourseChoiceGroup> choiceGroups = new List<CourseChoiceGroup>();
        List<OracleGroup> oracleGroups = new List<OracleGroup>();

        for (int groupIndex = 0; groupIndex < 3; ++groupIndex)
        {
            int courseCandidateCount = groupIndex == 1 ? 2 : 1;
            List<CourseCandidate> courseCandidates = new List<CourseCandidate>();
            List<OracleCandidate> oracleCandidates = new List<OracleCandidate>();

            for (int courseIndex = 0;
                courseIndex < courseCandidateCount;
                ++courseIndex)
            {
                string courseCodeValue = createCourseCode(groupIndex, courseIndex);
                CatalogCourse course =
                    ScheduleRecommendationTestData.CreateCourse(courseCodeValue);
                List<OfferingCandidate> candidateOfferings =
                    new List<OfferingCandidate>();
                courses.Add(course);
                addOffering(
                    random,
                    courseCodeValue,
                    groupIndex,
                    "01",
                    EOfferingPreference.Preferred,
                    offerings,
                    candidateOfferings,
                    oracleCandidates);
                addOffering(
                    random,
                    courseCodeValue,
                    groupIndex,
                    "02",
                    EOfferingPreference.Acceptable,
                    offerings,
                    candidateOfferings,
                    oracleCandidates);
                addOffering(
                    random,
                    courseCodeValue,
                    groupIndex,
                    "03",
                    EOfferingPreference.Excluded,
                    offerings,
                    candidateOfferings,
                    oracleCandidates);
                courseCandidates.Add(new CourseCandidate(
                    course.Id,
                    candidateOfferings));
            }

            choiceGroups.Add(new CourseChoiceGroup(
                CourseChoiceGroupId.CreateNew(),
                ECourseChoiceCardinality.ExactlyOne,
                courseCandidates));
            oracleGroups.Add(new OracleGroup(oracleCandidates));
        }

        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            courses,
            offerings);
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            choiceGroups,
            Array.Empty<UnscheduledOfferingSelection>());
        return new OracleFixture(catalog, plan, oracleGroups);
    }

    private static void addOffering(
        Random random,
        string courseCodeValue,
        int groupIndex,
        string sectionCodeValue,
        EOfferingPreference preference,
        ICollection<CatalogOffering> catalogOfferings,
        ICollection<OfferingCandidate> candidateOfferings,
        ICollection<OracleCandidate> oracleCandidates)
    {
        CatalogOffering catalogOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                courseCodeValue,
                sectionCodeValue,
                createMeetingSlots(random, groupIndex, sectionCodeValue));
        catalogOfferings.Add(catalogOffering);
        candidateOfferings.Add(new OfferingCandidate(
            catalogOffering.Id,
            preference));
        if (preference != EOfferingPreference.Excluded)
        {
            oracleCandidates.Add(new OracleCandidate(
                new ScheduledOffering(catalogOffering),
                preference));
        }
    }

    private static MeetingSlot[] createMeetingSlots(
        Random random,
        int groupIndex,
        string sectionCodeValue)
    {
        if (sectionCodeValue == "01")
        {
            MeetingSlot sharedConflict =
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1);
            EDay supplementaryDay = (EDay)random.Next(
                (int)EDay.Tuesday,
                (int)EDay.Friday + 1);
            MeetingSlot supplementarySlot =
                ScheduleRecommendationTestData.CreateMeetingSlot(
                    supplementaryDay,
                    random.Next(2, 7));
            return new MeetingSlot[] { sharedConflict, supplementarySlot };
        }

        if (sectionCodeValue == "02")
        {
            EDay nonConflictingDay = (EDay)((int)EDay.Tuesday + groupIndex);
            return new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(
                    nonConflictingDay,
                    7),
            };
        }

        EDay excludedDay = (EDay)random.Next(
            (int)EDay.Monday,
            (int)EDay.Friday + 1);
        return new MeetingSlot[]
        {
            ScheduleRecommendationTestData.CreateMeetingSlot(
                excludedDay,
                random.Next(1, 8)),
        };
    }

    private static string createCourseCode(int groupIndex, int courseIndex)
    {
        int numericCode = checked((groupIndex * 10) + courseIndex);
        return "ORA" + numericCode.ToString("D5", CultureInfo.InvariantCulture);
    }

    private static ScheduleRecommendationResult generate(OracleFixture fixture)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            fixture.Catalog,
            fixture.Plan,
            new ScheduleRecommendationLimit(MAXIMUM_RECOMMENDATION_COUNT));
        ScheduleRecommendationGenerator generator =
            new ScheduleRecommendationGenerator();
        return generator.GenerateRecommendations(request, CancellationToken.None);
    }

    private static Dictionary<string, int> createExpectedScoresByKey(
        IEnumerable<OracleSolution> solutions)
    {
        Dictionary<string, int> scoresByKey =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (OracleSolution solution in solutions)
        {
            scoresByKey.Add(solution.Key, solution.Score);
        }

        return scoresByKey;
    }

    private static string createOfferingKey(
        IEnumerable<ScheduledOffering> offerings)
    {
        List<string> offeringIds = new List<string>();
        foreach (ScheduledOffering offering in offerings)
        {
            offeringIds.Add(offering.OfferingId.Value);
        }

        return string.Join("|", offeringIds);
    }

    private static string[] serializeRecommendations(
        IEnumerable<ScheduleRecommendation> recommendations)
    {
        List<string> serializedRecommendations = new List<string>();
        foreach (ScheduleRecommendation recommendation in recommendations)
        {
            serializedRecommendations.Add(
                recommendation.Score.Value.ToString(CultureInfo.InvariantCulture)
                + ":"
                + createOfferingKey(recommendation.ScheduledOfferings));
        }

        return serializedRecommendations.ToArray();
    }

    private static int getCartesianCombinationCount(
        IEnumerable<OracleGroup> groups)
    {
        int combinationCount = 1;
        foreach (OracleGroup group in groups)
        {
            combinationCount = checked(combinationCount * group.Candidates.Count);
        }

        return combinationCount;
    }

    private static bool containsDifferentScores(
        IReadOnlyList<OracleSolution> solutions)
    {
        int firstScore = solutions[0].Score;
        foreach (OracleSolution solution in solutions)
        {
            if (solution.Score != firstScore)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class OracleFixture
    {
        public CourseCatalog Catalog { get; }

        public PlanningPlan Plan { get; }

        public IReadOnlyList<OracleGroup> Groups { get; }

        public OracleFixture(
            CourseCatalog catalog,
            PlanningPlan plan,
            IReadOnlyList<OracleGroup> groups)
        {
            Catalog = catalog;
            Plan = plan;
            Groups = groups;
        }
    }

    private sealed class OracleGroup
    {
        public IReadOnlyList<OracleCandidate> Candidates { get; }

        public OracleGroup(IEnumerable<OracleCandidate> candidates)
        {
            List<OracleCandidate> copiedCandidates =
                new List<OracleCandidate>(candidates);
            Candidates = copiedCandidates.AsReadOnly();
        }
    }

    private sealed class OracleCandidate
    {
        public ScheduledOffering Offering { get; }

        public EOfferingPreference Preference { get; }

        public OracleCandidate(
            ScheduledOffering offering,
            EOfferingPreference preference)
        {
            Offering = offering;
            Preference = preference;
        }
    }

    private sealed class OracleSolution
    {
        public string Key { get; }

        public int Score { get; }

        public OracleSolution(string key, int score)
        {
            Key = key;
            Score = score;
        }
    }
}
