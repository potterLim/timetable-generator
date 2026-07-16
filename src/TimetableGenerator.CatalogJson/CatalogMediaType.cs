using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogMediaType
{
    public string Value { get; }

    public CatalogMediaType(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog media types");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
