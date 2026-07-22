using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

[TestClass]
public sealed class VerifiedCatalogPackageTests
{
    [TestMethod]
    public void PackageCreatesPlanBindingPinnedToVerifiedArtifactSha256()
    {
        VerifiedCatalogPackage package = CatalogSynchronizationTestDocuments.CreateVerifiedPackage();

        PlanCatalogBinding binding = package.CreatePlanCatalogBinding();

        Assert.AreEqual(package.Entry.CatalogId, binding.CatalogId);
        Assert.AreEqual(package.Entry.Institution.Id, binding.InstitutionId);
        Assert.AreEqual(package.Entry.Term, binding.Term);
        Assert.AreEqual(package.Entry.Revision, binding.Revision);
        Assert.AreEqual(
            new CatalogArtifactSha256(package.Entry.File.Sha256.HexValue),
            binding.ArtifactSha256);
    }
}
