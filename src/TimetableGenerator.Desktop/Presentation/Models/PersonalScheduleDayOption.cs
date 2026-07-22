using System;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PersonalScheduleDayOption : ObservableObject
{
    private bool mIsSelected;

    public EDay Day { get; }

    public string ShortName
    {
        get
        {
            return findShortName(Day);
        }
    }

    public string AccessibleName
    {
        get
        {
            return findAccessibleName(Day);
        }
    }

    public string AutomationId
    {
        get
        {
            return "PersonalSchedule" + Day + "Input";
        }
    }

    public bool IsSelected
    {
        get
        {
            return mIsSelected;
        }
        set
        {
            if (setProperty(ref mIsSelected, value))
            {
                EventHandler? selectionChangedOrNull = SelectionChanged;
                if (selectionChangedOrNull != null)
                {
                    selectionChangedOrNull(this, EventArgs.Empty);
                }
            }
        }
    }

    public event EventHandler? SelectionChanged;

    public PersonalScheduleDayOption(EDay day)
    {
        ensureSupportedDay(day);
        Day = day;
    }

    private static string findShortName(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월";
            case EDay.Tuesday:
                return "화";
            case EDay.Wednesday:
                return "수";
            case EDay.Thursday:
                return "목";
            case EDay.Friday:
                return "금";
            case EDay.Saturday:
                return "토";
            case EDay.Sunday:
                return "일";
            default:
                throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown personal schedule day.");
        }
    }

    private static string findAccessibleName(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월요일";
            case EDay.Tuesday:
                return "화요일";
            case EDay.Wednesday:
                return "수요일";
            case EDay.Thursday:
                return "목요일";
            case EDay.Friday:
                return "금요일";
            case EDay.Saturday:
                return "토요일";
            case EDay.Sunday:
                return "일요일";
            default:
                throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown personal schedule day.");
        }
    }

    private static void ensureSupportedDay(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
            case EDay.Saturday:
            case EDay.Sunday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Personal schedule day options require a day from Monday through Sunday.");
        }
    }
}
