using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogFileExtension
{
    public string Value { get; }

    public CatalogFileExtension(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog file extensions");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
