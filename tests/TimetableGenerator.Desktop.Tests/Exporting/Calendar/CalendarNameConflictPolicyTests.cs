using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class CalendarNameConflictPolicyTests
{
    [Fact]
    public void NameMatchingUsesTrimmedUnicodeFormCAndAsciiCaseFolding()
    {
        PlanName composedName = new PlanName("  Caf\u00E9  ");
        PlanName decomposedName = new PlanName("cafe\u0301");

        bool isSameName = CalendarNameConflictPolicy.IsSameName(composedName, decomposedName);

        Assert.True(isSameName);
        Assert.True(CalendarNameConflictPolicy.IsNameInUse(composedName, new PlanName[] { decomposedName }));
    }

    [Theory]
    [InlineData("Straße", "STRAßE", true)]
    [InlineData("Straße", "STRASSE", false)]
    [InlineData("ı", "I", false)]
    [InlineData("ﬀ", "FF", false)]
    public void NonAsciiCharactersHaveDeterministicOrdinalIdentity(string firstValue, string secondValue, bool expectedMatch)
    {
        bool isSameName = CalendarNameConflictPolicy.IsSameName(new PlanName(firstValue), new PlanName(secondValue));

        Assert.Equal(expectedMatch, isSameName);
    }

    [Fact]
    public void CanonicalNameMatchesTheNativeAsciiOnlyContract()
    {
        Assert.Equal("STRAßE", CalendarNameConflictPolicy.normalizeName("  Straße  "));
        Assert.Equal("Iı", CalendarNameConflictPolicy.normalizeName("iı"));
        Assert.Equal("FFﬀ", CalendarNameConflictPolicy.normalizeName("ffﬀ"));
    }

    [Fact]
    public void NextAvailableNameUsesTheFirstFreeNumberedSuffix()
    {
        PlanName requestedName = new PlanName("Caf\u00E9 timetable");
        PlanName[] existingNames = new PlanName[]
        {
            new PlanName("CAF\u00E9 TIMETABLE"),
            new PlanName("Caf\u00E9 timetable (2)"),
            new PlanName("Cafe\u0301 timetable (3)"),
        };

        PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(requestedName, existingNames);

        Assert.Equal("Caf\u00E9 timetable (4)", nextAvailableName.Value);
    }

    [Fact]
    public void NextAvailableNameStartsAtTwo()
    {
        PlanName requestedName = new PlanName("2026-2학기 시간표");

        PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(requestedName, new PlanName[] { requestedName });

        Assert.Equal("2026-2학기 시간표 (2)", nextAvailableName.Value);
    }

    [Fact]
    public void NextAvailableNameRespectsThePlanNameLengthLimit()
    {
        PlanName requestedName = new PlanName(new string('가', PlanName.MAXIMUM_LENGTH));

        PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(requestedName, new PlanName[] { requestedName });

        Assert.Equal(PlanName.MAXIMUM_LENGTH, nextAvailableName.Value.Length);
        Assert.Equal(new string('가', PlanName.MAXIMUM_LENGTH - " (2)".Length) + " (2)", nextAvailableName.Value);
    }

    [Fact]
    public void NameTruncationDoesNotSplitAUnicodeTextElement()
    {
        string requestedNameValue = new string('a', 75) + "\U0001F600bbb";
        PlanName requestedName = new PlanName(requestedNameValue);

        PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(requestedName, new PlanName[] { requestedName });

        Assert.Equal(new string('a', 75) + " (2)", nextAvailableName.Value);
        Assert.DoesNotContain('\uD83D', nextAvailableName.Value);
        Assert.DoesNotContain('\uDE00', nextAvailableName.Value);
    }

    [Fact]
    public void LongerCopyNumbersReserveEnoughSuffixSpace()
    {
        PlanName requestedName = new PlanName(new string('a', PlanName.MAXIMUM_LENGTH));
        List<PlanName> existingNames = new List<PlanName> { requestedName };
        for (int copyNumber = 2; copyNumber <= 10; ++copyNumber)
        {
            string suffix = " (" + copyNumber + ")";
            existingNames.Add(new PlanName(new string('a', PlanName.MAXIMUM_LENGTH - suffix.Length) + suffix));
        }

        PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(requestedName, existingNames);

        Assert.Equal(new string('a', PlanName.MAXIMUM_LENGTH - " (11)".Length) + " (11)", nextAvailableName.Value);
        Assert.Equal(PlanName.MAXIMUM_LENGTH, nextAvailableName.Value.Length);
    }

    [Fact]
    public void NamePolicyRejectsMissingNames()
    {
        PlanName calendarName = new PlanName("시간표");

        Assert.Throws<ArgumentNullException>(
            () => CalendarNameConflictPolicy.IsSameName(null!, calendarName));
        Assert.Throws<ArgumentNullException>(
            () => CalendarNameConflictPolicy.IsSameName(calendarName, null!));
        Assert.Throws<ArgumentNullException>(
            () => CalendarNameConflictPolicy.IsNameInUse(calendarName, null!));
        Assert.Throws<ArgumentNullException>(
            () => CalendarNameConflictPolicy.FindNextAvailableName(
                null!,
                Array.Empty<PlanName>()));
        Assert.Throws<ArgumentException>(
            () => CalendarNameConflictPolicy.FindNextAvailableName(
                calendarName,
                new PlanName[] { null! }));
    }

    [Theory]
    [InlineData((int)ECalendarExportProvider.Google)]
    [InlineData((int)ECalendarExportProvider.Apple)]
    public void ConflictCarriesProviderNamesAndReplacementAvailability(int providerValue)
    {
        ECalendarExportProvider provider = (ECalendarExportProvider)providerValue;
        PlanName requestedName = new PlanName("시간표");
        PlanName nextAvailableName = new PlanName("시간표 (2)");

        CalendarNameConflict conflict = new CalendarNameConflict(provider, requestedName, nextAvailableName, ECalendarReplacementAvailability.Available);

        Assert.Equal(provider, conflict.Provider);
        Assert.Same(requestedName, conflict.RequestedName);
        Assert.Same(nextAvailableName, conflict.NextAvailableName);
        Assert.Equal(ECalendarReplacementAvailability.Available, conflict.ReplacementAvailability);
        Assert.True(conflict.CanReplace);
    }

    [Fact]
    public void ConflictRejectsInvalidState()
    {
        PlanName requestedName = new PlanName("Caf\u00E9");
        PlanName nextAvailableName = new PlanName("Caf\u00E9 (2)");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CalendarNameConflict(
                ECalendarExportProvider.None,
                requestedName,
                nextAvailableName,
                ECalendarReplacementAvailability.Available));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CalendarNameConflict(
                (ECalendarExportProvider)99,
                requestedName,
                nextAvailableName,
                ECalendarReplacementAvailability.Available));
        Assert.Throws<ArgumentNullException>(
            () => new CalendarNameConflict(
                ECalendarExportProvider.Google,
                null!,
                nextAvailableName,
                ECalendarReplacementAvailability.Available));
        Assert.Throws<ArgumentNullException>(
            () => new CalendarNameConflict(
                ECalendarExportProvider.Google,
                requestedName,
                null!,
                ECalendarReplacementAvailability.Available));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CalendarNameConflict(
                ECalendarExportProvider.Google,
                requestedName,
                nextAvailableName,
                (ECalendarReplacementAvailability)99));
        Assert.Throws<ArgumentException>(
            () => new CalendarNameConflict(
                ECalendarExportProvider.Google,
                requestedName,
                new PlanName("cafe\u0301"),
                ECalendarReplacementAvailability.Available));
    }

    [Theory]
    [InlineData((int)ECalendarNameConflictResolution.ReplaceExisting)]
    [InlineData((int)ECalendarNameConflictResolution.CreateWithAvailableName)]
    [InlineData((int)ECalendarNameConflictResolution.Cancel)]
    public void AvailableReplacementSupportsEveryExplicitResolution(int resolutionValue)
    {
        ECalendarNameConflictResolution resolution = (ECalendarNameConflictResolution)resolutionValue;
        CalendarNameConflict conflict = createConflict(ECalendarReplacementAvailability.Available);

        CalendarNameConflictPolicy.EnsureResolutionIsSupported(conflict, resolution);
    }

    [Fact]
    public void UnavailableReplacementRejectsReplaceButAllowsSafeChoices()
    {
        CalendarNameConflict conflict = createConflict(ECalendarReplacementAvailability.Unavailable);

        Assert.Throws<InvalidOperationException>(
            () => CalendarNameConflictPolicy.EnsureResolutionIsSupported(
                conflict,
                ECalendarNameConflictResolution.ReplaceExisting));
        CalendarNameConflictPolicy.EnsureResolutionIsSupported(conflict, ECalendarNameConflictResolution.CreateWithAvailableName);
        CalendarNameConflictPolicy.EnsureResolutionIsSupported(conflict, ECalendarNameConflictResolution.Cancel);
    }

    [Fact]
    public void ResolutionValidationRejectsMissingOrUnknownResolution()
    {
        CalendarNameConflict conflict = createConflict(ECalendarReplacementAvailability.Available);

        Assert.Throws<ArgumentNullException>(
            () => CalendarNameConflictPolicy.EnsureResolutionIsSupported(
                null!,
                ECalendarNameConflictResolution.Cancel));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CalendarNameConflictPolicy.EnsureResolutionIsSupported(
                conflict,
                ECalendarNameConflictResolution.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CalendarNameConflictPolicy.EnsureResolutionIsSupported(
                conflict,
                (ECalendarNameConflictResolution)99));
    }

    private static CalendarNameConflict createConflict(ECalendarReplacementAvailability replacementAvailability)
    {
        return new CalendarNameConflict(ECalendarExportProvider.Google, new PlanName("시간표"), new PlanName("시간표 (2)"), replacementAvailability);
    }
}
