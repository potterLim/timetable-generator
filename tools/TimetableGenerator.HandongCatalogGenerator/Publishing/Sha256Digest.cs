using System;
using System.Security.Cryptography;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal readonly record struct Sha256Digest
{
    private const int HEX_CHARACTER_COUNT = 64;

    public string HexValue { get; }

    private Sha256Digest(string hexValue)
    {
        HexValue = hexValue;
    }

    public static Sha256Digest Compute(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);

        return new Sha256Digest(Convert.ToHexStringLower(hash));
    }

    public static Sha256Digest Parse(string value)
    {
        if (value.Length != HEX_CHARACTER_COUNT)
        {
            throw new FormatException("A SHA-256 digest must contain 64 hexadecimal characters.");
        }

        byte[] parsedBytes;
        try
        {
            parsedBytes = Convert.FromHexString(value);
        }
        catch (FormatException error)
        {
            throw new FormatException("The SHA-256 digest contains an invalid character.", error);
        }

        return new Sha256Digest(Convert.ToHexStringLower(parsedBytes));
    }

    public override string ToString()
    {
        return HexValue;
    }
}
