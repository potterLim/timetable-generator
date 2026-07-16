namespace TimetableGenerator.Application.Planning;

public enum EPlanningCatalogTransitionStatus
{
    ExactMatch = 0,
    UpgradeEligible = 1,
    InstitutionMismatch = 2,
    AcademicTermMismatch = 3,
    RevisionNotNewer = 4,
    ArtifactSha256Mismatch = 5,
}
