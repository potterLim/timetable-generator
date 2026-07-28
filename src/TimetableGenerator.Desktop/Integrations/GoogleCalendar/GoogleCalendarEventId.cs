using System;
using System.Security.Cryptography;
using System.Text;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarEventId
{
    private const string BASE32_HEX_ALPHABET = "0123456789abcdefghijklmnopqrstuv";

    public string Value { get; }

    private GoogleCalendarEventId(string value)
    {
        Value = value;
    }

    public static GoogleCalendarEventId Create(PlanId planId, GoogleCalendarSourceEventId sourceEventId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException("Google Calendar event IDs require a valid plan ID.", nameof(planId));
        }

        if (sourceEventId == null)
        {
            throw new ArgumentNullException(nameof(sourceEventId));
        }

        string identity = planId.Value.ToString("N") + ":" + sourceEventId.Value;
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new GoogleCalendarEventId("tg" + encodeBase32Hex(digest));
    }

    public override string ToString()
    {
        return Value;
    }

    internal static GoogleCalendarEventId createFromExisting(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length < 5 || normalizedValue.Length > 1024)
        {
            throw new ArgumentException("Google Calendar event IDs must contain between 5 and 1024 characters.", nameof(value));
        }

        foreach (char character in normalizedValue)
        {
            bool isValid = character is >= 'a' and <= 'v' || character is >= '0' and <= '9';
            if (isValid == false)
            {
                throw new ArgumentException("Google Calendar event IDs must use base32hex characters.", nameof(value));
            }
        }

        return new GoogleCalendarEventId(normalizedValue);
    }

    private static string encodeBase32Hex(byte[] value)
    {
        StringBuilder builder = new StringBuilder((value.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsInBuffer = 0;
        foreach (byte currentByte in value)
        {
            buffer = (buffer << 8) | currentByte;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                int alphabetIndex = (buffer >> bitsInBuffer) & 0x1F;
                builder.Append(BASE32_HEX_ALPHABET[alphabetIndex]);
            }
        }

        if (bitsInBuffer > 0)
        {
            int alphabetIndex = (buffer << (5 - bitsInBuffer)) & 0x1F;
            builder.Append(BASE32_HEX_ALPHABET[alphabetIndex]);
        }

        return builder.ToString();
    }
}
