using Avalonia.Headless;

using TimetableGenerator.Desktop.Tests;

using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
