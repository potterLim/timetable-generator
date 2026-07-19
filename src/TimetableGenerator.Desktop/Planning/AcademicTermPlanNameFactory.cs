using System;
using System.Collections.Generic;
using System.Globalization;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Planning;

internal static class AcademicTermPlanNameFactory
{
    private const int FIRST_ADDITIONAL_PLAN_NUMBER = 2;

    private const string PLAN_NAME_SUFFIX = "학기 시간표";

    public static PlanName CreateInitialPlanName(AcademicTerm academicTerm)
    {
        requireValidAcademicTerm(academicTerm);
        return new PlanName(academicTerm.Id + PLAN_NAME_SUFFIX);
    }

    public static PlanName FindAvailablePlanName(
        AcademicTerm academicTerm,
        IReadOnlyList<PlanningPlan> existingPlans)
    {
        requireValidAcademicTerm(academicTerm);
        if (existingPlans == null)
        {
            throw new ArgumentNullException(nameof(existingPlans));
        }

        HashSet<string> existingPlanNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (PlanningPlan plan in existingPlans)
        {
            if (plan == null)
            {
                throw new ArgumentException(
                    "Existing plans cannot contain null values.",
                    nameof(existingPlans));
            }

            existingPlanNames.Add(plan.Name.Value);
        }

        PlanName initialPlanName = CreateInitialPlanName(academicTerm);
        if (existingPlanNames.Contains(initialPlanName.Value) == false)
        {
            return initialPlanName;
        }

        int planNumber = FIRST_ADDITIONAL_PLAN_NUMBER;
        while (true)
        {
            PlanName candidateName = new PlanName(
                academicTerm.Id
                + PLAN_NAME_SUFFIX
                + "("
                + planNumber.ToString(CultureInfo.InvariantCulture)
                + ")");
            if (existingPlanNames.Contains(candidateName.Value) == false)
            {
                return candidateName;
            }

            ++planNumber;
        }
    }

    private static void requireValidAcademicTerm(AcademicTerm academicTerm)
    {
        if (academicTerm.IsValid == false)
        {
            throw new ArgumentException(
                "Plan names require a valid academic term.",
                nameof(academicTerm));
        }
    }
}
