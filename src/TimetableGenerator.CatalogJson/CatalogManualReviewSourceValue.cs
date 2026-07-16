using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogManualReviewSourceValue
{
    public string Value { get; }

    public CatalogManualReviewSourceValue(string value)
    {
        CatalogTextValueValidation.requireNonBlank(value, "Manual review source values");
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
