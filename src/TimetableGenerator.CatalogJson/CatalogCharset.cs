using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogCharset
{
    public string Value { get; }

    public CatalogCharset(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog character sets");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
