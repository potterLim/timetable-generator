using System;
using System.Collections.Generic;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningWorkspace
{
    private readonly IReadOnlyList<PlanningPlan> mPlans;

    public PlanCatalogBinding CatalogBinding { get; }

    public PlanId? ActivePlanIdOrNull { get; }

    public bool HasPlans
    {
        get
        {
            return mPlans.Count > 0;
        }
    }

    public IReadOnlyList<PlanningPlan> Plans
    {
        get
        {
            return mPlans;
        }
    }

    public PlanningWorkspace(
        PlanCatalogBinding catalogBinding,
        PlanId? activePlanIdOrNull,
        IEnumerable<PlanningPlan> plans)
    {
        if (catalogBinding == null)
        {
            throw new ArgumentNullException(nameof(catalogBinding));
        }

        if (activePlanIdOrNull.HasValue && activePlanIdOrNull.Value.IsValid == false)
        {
            throw new ArgumentException(
                "Planning workspaces require a valid active plan ID when one is set.",
                nameof(activePlanIdOrNull));
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
                throw new ArgumentException("Planning workspaces cannot contain null plans.", nameof(plans));
            }

            if (planIds.Add(plan.Id) == false)
            {
                throw new ArgumentException(
                    "Planning workspaces cannot contain duplicate plan IDs.",
                    nameof(plans));
            }

            if (plan.CatalogBinding != catalogBinding)
            {
                throw new ArgumentException(
                    "Every workspace plan must use the workspace catalog binding.",
                    nameof(plans));
            }

            if (planNames.Add(plan.Name.Value) == false)
            {
                throw new ArgumentException(
                    "Planning workspaces cannot contain duplicate plan names.",
                    nameof(plans));
            }

            if (activePlanIdOrNull.HasValue && plan.Id == activePlanIdOrNull.Value)
            {
                hasActivePlan = true;
            }

            copiedPlans.Add(plan);
        }

        if (copiedPlans.Count == 0)
        {
            if (activePlanIdOrNull.HasValue)
            {
                throw new ArgumentException(
                    "Empty planning workspaces cannot identify an active plan.",
                    nameof(activePlanIdOrNull));
            }
        }
        else if (activePlanIdOrNull.HasValue == false)
        {
            throw new ArgumentException(
                "Planning workspaces with plans require an active plan ID.",
                nameof(activePlanIdOrNull));
        }
        else if (hasActivePlan == false)
        {
            throw new ArgumentException(
                "The active plan ID must identify a workspace plan.",
                nameof(activePlanIdOrNull));
        }

        CatalogBinding = catalogBinding;
        ActivePlanIdOrNull = activePlanIdOrNull;
        mPlans = copiedPlans.AsReadOnly();
    }

    public PlanningPlan GetActivePlan()
    {
        if (ActivePlanIdOrNull.HasValue == false)
        {
            throw new InvalidOperationException("The planning workspace does not contain an active plan.");
        }

        foreach (PlanningPlan plan in mPlans)
        {
            if (plan.Id == ActivePlanIdOrNull.Value)
            {
                return plan;
            }
        }

        throw new InvalidOperationException(
            "The planning workspace invariant no longer contains its active plan.");
    }
}
