using System;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView : UserControl
{
    private readonly ScheduleWorkspaceView mScheduleWorkspaceView;

    public ProductWorkspaceHostView()
    {
        AvaloniaXamlLoader.Load(this);
        mScheduleWorkspaceView = this.FindControl<ScheduleWorkspaceView>("ScheduleWorkspace") ?? throw new InvalidOperationException("The schedule workspace could not be created.");
        DataContextChanged += onDataContextChanged;
        AttachedToVisualTree += onAttachedToVisualTree;
        DetachedFromVisualTree += onDetachedFromVisualTree;
        AddHandler(KeyDownEvent, onKeyDown, RoutingStrategies.Tunnel);
    }

    internal void beginExportShutdown()
    {
        mScheduleWorkspaceView.beginExportShutdown();
    }

    internal void blockNewExportsForShutdown()
    {
        mScheduleWorkspaceView.blockNewExportsForShutdown();
    }

    internal Task completeExportShutdownAsync(CancellationToken cancellationToken)
    {
        return mScheduleWorkspaceView.completeExportShutdownAsync(cancellationToken);
    }

    internal void cancelExportShutdown()
    {
        mScheduleWorkspaceView.cancelExportShutdown();
    }
}
