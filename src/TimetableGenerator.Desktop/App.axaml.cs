using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Desktop.Presentation.Sample;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

namespace TimetableGenerator.Desktop;

internal sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull =
            ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktopLifetimeOrNull != null)
        {
            PlannerWorkspaceViewModel workspaceViewModel = PlannerSampleStateFactory.CreateWorkspace();
            desktopLifetimeOrNull.MainWindow = new MainWindow(workspaceViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
