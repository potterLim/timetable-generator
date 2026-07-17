using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductTimePickerTests
{
    private sealed class ScheduleTimeBindingSource : ObservableObject
    {
        private ScheduleTime? mSelectedTimeOrNull;

        public ScheduleTime? SelectedTimeOrNull
        {
            get
            {
                return mSelectedTimeOrNull;
            }
            set
            {
                setProperty(ref mSelectedTimeOrNull, value);
            }
        }
    }

    [AvaloniaFact]
    public void TwelveHourSegmentsProduceStronglyTypedScheduleTimes()
    {
        ProductTimePicker timePicker = new ProductTimePicker();
        Window window = new Window();
        window.Content = timePicker;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ComboBox[] segments = timePicker.GetVisualDescendants()
                .OfType<ComboBox>()
                .ToArray();

            Assert.Equal(3, segments.Length);
            Assert.Equal(
                "시간 오전 또는 오후",
                AutomationProperties.GetName(segments[0]));
            Assert.Equal("시간 시", AutomationProperties.GetName(segments[1]));
            Assert.Equal("시간 분", AutomationProperties.GetName(segments[2]));
            Assert.True(timePicker.MinWidth >= 280.0);
            Assert.All(
                segments,
                segment => Assert.True(segment.MinHeight >= 42.0));
            Assert.All(
                segments,
                segment => Assert.Equal(
                    VerticalAlignment.Center,
                    segment.VerticalContentAlignment));
            Assert.All(
                segments,
                segment => Assert.Equal(
                    HorizontalAlignment.Center,
                    segment.HorizontalContentAlignment));

            segments[0].SelectedIndex = 1;
            segments[1].SelectedIndex = 1;
            segments[2].SelectedIndex = 6;

            Assert.Equal(new ScheduleTime(14, 30), timePicker.SelectedTimeOrNull);

            segments[0].SelectedIndex = 0;
            segments[1].SelectedIndex = 11;
            segments[2].SelectedIndex = 0;

            Assert.Equal(new ScheduleTime(0, 0), timePicker.SelectedTimeOrNull);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RepeatedInputChangesPreserveTheTwoWayBinding()
    {
        ScheduleTimeBindingSource bindingSource = new ScheduleTimeBindingSource();
        bindingSource.SelectedTimeOrNull = new ScheduleTime(9, 15);
        ProductTimePicker timePicker = new ProductTimePicker();
        timePicker.DataContext = bindingSource;
        timePicker.Bind(
            ProductTimePicker.SelectedTimeOrNullProperty,
            new Binding(nameof(ScheduleTimeBindingSource.SelectedTimeOrNull))
            {
                Mode = BindingMode.TwoWay,
            });
        Window window = new Window();
        window.Content = timePicker;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ComboBox[] segments = timePicker.GetVisualDescendants()
                .OfType<ComboBox>()
                .ToArray();

            segments[0].SelectedIndex = 1;
            segments[1].SelectedIndex = 1;
            segments[2].SelectedIndex = 6;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new ScheduleTime(14, 30), bindingSource.SelectedTimeOrNull);

            bindingSource.SelectedTimeOrNull = new ScheduleTime(7, 45);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, segments[0].SelectedIndex);
            Assert.Equal(6, segments[1].SelectedIndex);
            Assert.Equal(9, segments[2].SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }
}
