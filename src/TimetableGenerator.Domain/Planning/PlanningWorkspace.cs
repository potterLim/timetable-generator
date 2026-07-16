using System;
using System.Collections.Generic;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningWorkspace
{
    private readonly IReadOnlyList<PlanningPlan> mPlans;

    public PlanId ActivePlanId { get; }

    public IReadOnlyList<PlanningPlan> Plans
    {
        get
        {
            return mPlans;
        }
    }

    public PlanningWorkspace(PlanId activePlanId, IEnumerable<PlanningPlan> plans)
    {
        if (activePlanId.IsValid == false)
        {
            throw new ArgumentException(
                "Planning workspaces require a valid active plan ID.",
                nameof(activePlanId));
        }

        if (plans == null)
        {
            throw new ArgumentNullException(nameof(plans));
        }

        List<PlanningPlan> copiedPlans = new List<PlanningPlan>();
        HashSet<PlanId> planIds = new HashSet<PlanId>();
        HashSet<string> planNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasActivePlan = false;
        foreach (PlanningPlan plan in plans)
        {
            if (plan == null)
            {
                throw new ArgumentException(
                    "Planning workspaces cannot contain null plans.",
                    nameof(plans));
            }

            if (planIds.Add(plan.Id) == false)
            {
                throw new ArgumentException(
                    "Planning workspaces cannot contain duplicate plan IDs.",
                    nameof(plans));
            }

            if (planNames.Add(plan.Name.Value) == false)
            {
                throw new ArgumentException(
                    "Planning workspaces cannot contain duplicate plan names.",
                    nameof(plans));
            }

            if (plan.Id == activePlanId)
            {
                hasActivePlan = true;
            }

            copiedPlans.Add(plan);
        }

        if (copiedPlans.Count == 0)
        {
            throw new ArgumentException(
                "Planning workspaces require at least one plan.",
                nameof(plans));
        }

        if (hasActivePlan == false)
        {
            throw new ArgumentException(
                "The active plan ID must identify a workspace plan.",
                nameof(activePlanId));
        }

        ActivePlanId = activePlanId;
        mPlans = copiedPlans.AsReadOnly();
    }

    public PlanningPlan GetActivePlan()
    {
        foreach (PlanningPlan plan in mPlans)
        {
            if (plan.Id == ActivePlanId)
            {
                return plan;
            }
        }

        throw new InvalidOperationException(
            "The planning workspace invariant no longer contains its active plan.");
    }
}
