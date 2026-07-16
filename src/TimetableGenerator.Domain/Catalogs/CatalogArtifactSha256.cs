using System;

namespace TimetableGenerator.Domain.Catalogs;

public sealed record CatalogArtifactSha256
{
    private const int HEX_LENGTH = 64;

    public string HexValue { get; }

    public CatalogArtifactSha256(string hexValue)
    {
        if (hexValue == null)
        {
            throw new ArgumentNullException(nameof(hexValue));
        }

        if (hexValue.Length != HEX_LENGTH || containsInvalidCharacter(hexValue))
        {
            throw new ArgumentException(
                "Catalog artifact SHA-256 values must contain exactly 64 lowercase hexadecimal characters.",
                nameof(hexValue));
        }

        HexValue = hexValue;
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
