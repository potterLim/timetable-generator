using System;
using System.Security.Cryptography;

namespace TimetableGenerator.CatalogJson;

public sealed record Sha256Digest
{
    private const int HEX_LENGTH = 64;

    public string HexValue { get; }

    public Sha256Digest(string hexValue)
    {
        if (hexValue == null)
        {
            throw new ArgumentNullException(nameof(hexValue));
        }

        if (hexValue.Length != HEX_LENGTH || containsInvalidCharacter(hexValue))
        {
            throw new ArgumentException("SHA-256 digests must contain exactly 64 lowercase hexadecimal characters.", nameof(hexValue));
        }

        HexValue = hexValue;
    }

    public static Sha256Digest Compute(ReadOnlySpan<byte> content)
    {
        byte[] digestBytes = SHA256.HashData(content);
        string hexValue = Convert.ToHexStringLower(digestBytes);
        return new Sha256Digest(hexValue);
    }

    public bool Matches(ReadOnlySpan<byte> content)
    {
        Sha256Digest actualDigest = Compute(content);
        return string.Equals(HexValue, actualDigest.HexValue, StringComparison.Ordinal);
    }

    public override string ToString()
    {
        return HexValue;
    }

    private static bool containsInvalidCharacter(string hexValue)
    {
        foreach (char character in hexValue)
        {
            bool isDigit = character >= '0' && character <= '9';
            bool isLowercaseHexLetter = character >= 'a' && character <= 'f';
            if (isDigit == false && isLowercaseHexLetter == false)
            {
                return true;
            }
        }

        return false;
    }
}
