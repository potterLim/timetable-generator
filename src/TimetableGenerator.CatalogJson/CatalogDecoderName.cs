using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogDecoderName
{
    public string Value { get; }

    public CatalogDecoderName(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Catalog decoder names");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
