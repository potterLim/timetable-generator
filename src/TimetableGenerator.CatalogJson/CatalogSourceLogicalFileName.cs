using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogSourceLogicalFileName
{
    public string Value { get; }

    public CatalogSourceLogicalFileName(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog source logical file names");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
