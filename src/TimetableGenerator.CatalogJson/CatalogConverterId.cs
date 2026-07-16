using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogConverterId
{
    public string Value { get; }

    public CatalogConverterId(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog converter IDs");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
