using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongOfferingInformationNormalizer
{
    private static readonly Regex OFFERING_INFORMATION_FORMAT = new Regex(
        "^(?<unit>.+?)\\s*(?<session>주간|야간)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ADDITIONAL_INSTRUCTOR_FORMAT = new Regex(
        "외\\s*(?<count>[0-9]+)\\s*명$",
        RegexOptions.CultureInvariant);

    public HandongOfferingInformationNormalizationResult NormalizeOfferingInformation(
        HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(
            row,
            EHandongColumn.OfferingInformation);
        if (lines.Count == 0)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.OfferingInformation,
                "Offering unit and instruction session are required.");
        }

        Match offeringInformationMatch = OFFERING_INFORMATION_FORMAT.Match(lines[0]);
        if (offeringInformationMatch.Success == false)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.OfferingInformation,
                "The offering information must end with 주간 or 야간.");
        }

        OfferingUnitName offeringUnitName = new OfferingUnitName(
            offeringInformationMatch.Groups["unit"].Value);
        EInstructionSession instructionSession = parseInstructionSession(
            offeringInformationMatch.Groups["session"].Value,
            row);
        InstructorAssignment instructorAssignment = normalizeInstructorAssignment(lines, row);

        return new HandongOfferingInformationNormalizationResult(
            offeringUnitName,
            instructionSession,
            instructorAssignment);
    }

    private static EInstructionSession parseInstructionSession(
        string sourceValue,
        HandongRawOfferingRow row)
    {
        switch (sourceValue)
        {
            case "주간":
                return EInstructionSession.Daytime;
            case "야간":
                return EInstructionSession.Evening;
            default:
                throw new InvalidHandongSourceRecordException(
                    row.SourceRecordNumber,
                    EHandongColumn.OfferingInformation,
                    "Unsupported instruction session: " + sourceValue);
        }
    }

    private static InstructorAssignment normalizeInstructorAssignment(
        IReadOnlyList<string> lines,
        HandongRawOfferingRow row)
    {
        if (lines.Count == 1)
        {
            return InstructorAssignment.NotProvided;
        }

        string instructorDisplayValue = HandongCellValueReader.getCombinedText(lines, 1);
        if (string.Equals(
            instructorDisplayValue,
            "Unconfirmed",
            StringComparison.OrdinalIgnoreCase))
        {
            return InstructorAssignment.Unconfirmed;
        }

        Match additionalInstructorMatch = ADDITIONAL_INSTRUCTOR_FORMAT.Match(
            instructorDisplayValue);
        int additionalInstructorCountValue = 0;
        if (additionalInstructorMatch.Success)
        {
            bool isCountParsed = int.TryParse(
                additionalInstructorMatch.Groups["count"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out additionalInstructorCountValue);
            if (isCountParsed == false)
            {
                throw new InvalidHandongSourceRecordException(
                    row.SourceRecordNumber,
                    EHandongColumn.OfferingInformation,
                    "The additional instructor count is invalid.");
            }
        }

        InstructorDisplayText displayText = new InstructorDisplayText(instructorDisplayValue);
        AdditionalInstructorCount additionalInstructorCount = new AdditionalInstructorCount(
            additionalInstructorCountValue);
        return InstructorAssignment.CreateConfirmed(displayText, additionalInstructorCount);
    }
}
