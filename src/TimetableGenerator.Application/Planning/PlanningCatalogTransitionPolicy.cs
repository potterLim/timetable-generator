using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public static class PlanningCatalogTransitionPolicy
{
    public static EPlanningCatalogTransitionStatus EvaluateTransition(PlanCatalogBinding currentBinding, PlanCatalogBinding candidateBinding)
    {
        if (currentBinding == null)
        {
            throw new ArgumentNullException(nameof(currentBinding));
        }

        if (candidateBinding == null)
        {
            throw new ArgumentNullException(nameof(candidateBinding));
        }

        if (currentBinding == candidateBinding)
        {
            return EPlanningCatalogTransitionStatus.ExactMatch;
        }

        if (currentBinding.InstitutionId != candidateBinding.InstitutionId)
        {
            return EPlanningCatalogTransitionStatus.InstitutionMismatch;
        }

        if (currentBinding.Term != candidateBinding.Term)
        {
            return EPlanningCatalogTransitionStatus.AcademicTermMismatch;
        }

        bool isSameCatalogRevision = currentBinding.CatalogId == candidateBinding.CatalogId && currentBinding.Revision == candidateBinding.Revision;
        if (isSameCatalogRevision && currentBinding.ArtifactSha256 != candidateBinding.ArtifactSha256)
        {
            return EPlanningCatalogTransitionStatus.ArtifactSha256Mismatch;
        }

        if (candidateBinding.Revision.Value <= currentBinding.Revision.Value)
        {
            return EPlanningCatalogTransitionStatus.RevisionNotNewer;
        }

        return EPlanningCatalogTransitionStatus.UpgradeEligible;
    }
}
