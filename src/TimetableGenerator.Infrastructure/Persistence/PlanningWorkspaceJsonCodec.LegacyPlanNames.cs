using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private const int FIRST_ADDITIONAL_PLAN_NUMBER = 2;

    private const string LEGACY_INITIAL_PLAN_NAME = "나의 시간표";
    private const string TERM_PLAN_NAME_SUFFIX = "학기 시간표";

    private static void migrateLegacyPlanNames(List<PlanningPlan> plans)
    {
        HashSet<string> existingPlanNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PlanningPlan plan in plans)
        {
            existingPlanNames.Add(plan.Name.Value);
        }

        for (int planIndex = 0; planIndex < plans.Count; ++planIndex)
        {
            PlanningPlan plan = plans[planIndex];
            string? migratedNameOrNull = findMigratedPlanNameOrNull(plan);
            if (migratedNameOrNull == null || existingPlanNames.Contains(migratedNameOrNull))
            {
                continue;
            }

            existingPlanNames.Add(migratedNameOrNull);
            plans[planIndex] = new PlanningPlan(
                plan.Id,
                new PlanName(migratedNameOrNull),
                plan.CatalogBinding,
                plan.Content,
                plan.LastViewedRecommendationOrNull);
        }
    }

    private static string? findMigratedPlanNameOrNull(PlanningPlan plan)
    {
        string termPlanName = plan.CatalogBinding.Term.Id + TERM_PLAN_NAME_SUFFIX;
        if (plan.Name.Value == LEGACY_INITIAL_PLAN_NAME)
        {
            return termPlanName;
        }

        string numberedNamePrefix = termPlanName + " ";
        if (plan.Name.Value.StartsWith(numberedNamePrefix, StringComparison.Ordinal) == false)
        {
            return null;
        }

        string planNumberText = plan.Name.Value.Substring(numberedNamePrefix.Length);
        int planNumber;
        bool hasPlanNumber = int.TryParse(planNumberText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out planNumber);
        if (hasPlanNumber == false || planNumber < FIRST_ADDITIONAL_PLAN_NUMBER)
        {
            return null;
        }

        return termPlanName + " (" + planNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
    }
}
