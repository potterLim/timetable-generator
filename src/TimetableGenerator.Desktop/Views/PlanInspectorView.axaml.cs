using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class PlanInspectorView : UserControl
{
    public PlanInspectorView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void onPlanManagementActionClick(object? senderOrNull, RoutedEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(closePlanManagementFlyout, DispatcherPriority.Input);
    }

    private void closePlanManagementFlyout()
    {
        Button? managementButtonOrNull = this.FindControl<Button>("PlanManagementButton");
        managementButtonOrNull?.Flyout?.Hide();
        managementButtonOrNull?.Focus();
    }
}
