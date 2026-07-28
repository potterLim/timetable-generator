using System;

using TimetableGenerator.Desktop.Planning;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Planning;

public sealed class AcademicTermPlanNameFactoryTests
{
    [Fact]
    public void InitialPlanNameUsesTheProvidedAcademicTerm()
    {
        AcademicTerm academicTerm = AcademicTerm.Parse("2027-1");

        PlanName planName = AcademicTermPlanNameFactory.CreateInitialPlanName(academicTerm);

        Assert.Equal("2027-1학기 시간표", planName.Value);
    }

    [Fact]
    public void AvailablePlanNameUsesTheFirstAvailableTermBasedNumber()
    {
        AcademicTerm academicTerm = AcademicTerm.Parse("2027-1");
        PlanningPlan[] existingPlans = new PlanningPlan[]
        {
            createPlan(academicTerm, "2027-1학기 시간표"),
            createPlan(academicTerm, "2027-1학기 시간표 (2)"),
            createPlan(academicTerm, "2027-1학기 시간표 (3)"),
        };

        PlanName planName = AcademicTermPlanNameFactory.FindAvailablePlanName(academicTerm, existingPlans);

        Assert.Equal("2027-1학기 시간표 (4)", planName.Value);
    }

    [Fact]
    public void AvailablePlanNameUsesTheBaseNameWhenItIsAvailable()
    {
        AcademicTerm academicTerm = AcademicTerm.Parse("2027-1");
        PlanningPlan[] existingPlans = new PlanningPlan[]
        {
            createPlan(academicTerm, "2027-1학기 시간표 (2)"),
        };

        PlanName planName = AcademicTermPlanNameFactory.FindAvailablePlanName(academicTerm, existingPlans);

        Assert.Equal("2027-1학기 시간표", planName.Value);
    }

    [Fact]
    public void LegacyUnspacedPlanNameReservesTheSameCopyNumber()
    {
        AcademicTerm academicTerm = AcademicTerm.Parse("2027-1");
        PlanningPlan[] existingPlans = new PlanningPlan[]
        {
            createPlan(academicTerm, "2027-1학기 시간표"),
            createPlan(academicTerm, "2027-1학기 시간표(2)"),
        };

        PlanName planName = AcademicTermPlanNameFactory.FindAvailablePlanName(academicTerm, existingPlans);

        Assert.Equal("2027-1학기 시간표 (3)", planName.Value);
    }

    [Fact]
    public void PlanNameCreationRejectsInvalidInputs()
    {
        Assert.Throws<ArgumentException>(
            () => AcademicTermPlanNameFactory.CreateInitialPlanName(default));
        Assert.Throws<ArgumentException>(
            () => AcademicTermPlanNameFactory.FindAvailablePlanName(
                default,
                Array.Empty<PlanningPlan>()));
        Assert.Throws<ArgumentNullException>(
            () => AcademicTermPlanNameFactory.FindAvailablePlanName(
                AcademicTerm.Parse("2027-1"),
                null!));
    }

    private static PlanningPlan createPlan(AcademicTerm academicTerm, string planName)
    {
        PlanCatalogBinding catalogBinding = new PlanCatalogBinding(
            new CatalogId("catalog-test"),
            new InstitutionId("institution-test"),
            academicTerm,
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
        return new PlanningPlan(PlanId.CreateNew(), new PlanName(planName), catalogBinding, new PlanningPlanContent(Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>(), Array.Empty<PersonalSchedule>()));
    }
}
