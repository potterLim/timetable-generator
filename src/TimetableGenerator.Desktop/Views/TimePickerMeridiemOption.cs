using System;

namespace TimetableGenerator.Desktop.Views;

internal sealed class TimePickerMeridiemOption
{
    public ETimePickerMeridiem Meridiem { get; }

    public string DisplayName
    {
        get
        {
            switch (Meridiem)
            {
                case ETimePickerMeridiem.AnteMeridiem:
                    return "오전";
                case ETimePickerMeridiem.PostMeridiem:
                    return "오후";
                default:
                    throw new ArgumentOutOfRangeException(nameof(Meridiem), Meridiem, "Unknown time picker meridiem.");
            }
        }
    }

    public TimePickerMeridiemOption(ETimePickerMeridiem meridiem)
    {
        switch (meridiem)
        {
            case ETimePickerMeridiem.AnteMeridiem:
            case ETimePickerMeridiem.PostMeridiem:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(meridiem), meridiem, "Unknown time picker meridiem.");
        }

        Meridiem = meridiem;
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
