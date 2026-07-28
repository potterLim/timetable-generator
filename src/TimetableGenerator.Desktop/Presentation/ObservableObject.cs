using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimetableGenerator.Desktop.Presentation;

internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool setProperty<T>(ref T currentValue, T newValue, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return false;
        }

        currentValue = newValue;
        raisePropertyChanged(propertyName);
        return true;
    }

    protected void raisePropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChangedEventHandler? propertyChangedOrNull = PropertyChanged;
        if (propertyChangedOrNull != null)
        {
            propertyChangedOrNull(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
