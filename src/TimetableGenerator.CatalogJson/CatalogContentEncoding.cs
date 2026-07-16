using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogContentEncoding
{
    public string Value { get; }

    public CatalogContentEncoding(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog content encodings");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
