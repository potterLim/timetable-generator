namespace TimetableGenerator.Desktop.Presentation.Layout;

internal static class WorkspaceLayoutPolicy
{
    private const double EXTRA_WIDE_MINIMUM_WIDTH = 1_440.0;
    private const double WIDE_MINIMUM_WIDTH = 1_280.0;
    private const double MEDIUM_MINIMUM_WIDTH = 1_080.0;

    public static EWorkspaceLayoutMode FindLayoutMode(WorkspaceWidth workspaceWidth)
    {
        if (workspaceWidth.Value >= EXTRA_WIDE_MINIMUM_WIDTH)
        {
            return EWorkspaceLayoutMode.ExtraWide;
        }

        if (workspaceWidth.Value >= WIDE_MINIMUM_WIDTH)
        {
            return EWorkspaceLayoutMode.Wide;
        }

        if (workspaceWidth.Value >= MEDIUM_MINIMUM_WIDTH)
        {
            return EWorkspaceLayoutMode.Medium;
        }

        return EWorkspaceLayoutMode.Compact;
    }
}
