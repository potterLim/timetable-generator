namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class RequirementFilterOption
{
    public ERequirementFilter Value { get; }

    public string DisplayName { get; }

    public RequirementFilterOption(
        ERequirementFilter value,
        string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}
