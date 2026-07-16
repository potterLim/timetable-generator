namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseDepartmentFilterOption
{
    public ECourseDepartmentFilter Value { get; }

    public string DisplayName { get; }

    public CourseDepartmentFilterOption(
        ECourseDepartmentFilter value,
        string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}
