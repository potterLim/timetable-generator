using System;
using System.Windows.Input;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseOfferingPreferenceItem : ObservableObject
{
    private EOfferingPreference mPreference;

    public event EventHandler? PreferenceChanged;

    public CatalogCourseProjection CourseProjection { get; }

    public CatalogOfferingProjection Projection { get; }

    public string CourseName
    {
        get
        {
            return CourseProjection.Course.KoreanName.Value;
        }
    }

    public OfferingId OfferingId
    {
        get
        {
            return Projection.Offering.Id;
        }
    }

    public string SectionDisplayText
    {
        get
        {
            return Projection.Offering.SectionCode.Value + "분반";
        }
    }

    public string ScheduleDisplayText
    {
        get
        {
            return Projection.ScheduleSummary;
        }
    }

    public EnglishInstructionPercentage EnglishInstructionPercentage
    {
        get
        {
            return Projection.EnglishInstructionPercentage;
        }
    }

    public string EnglishInstructionDisplayText
    {
        get
        {
            return EnglishInstructionPercentageRange.CreateUniform(EnglishInstructionPercentage).DisplayText;
        }
    }

    public string EnglishInstructionAccessibleText
    {
        get
        {
            return EnglishInstructionPercentageRange.CreateUniform(EnglishInstructionPercentage).AccessibleText;
        }
    }

    public string InstructorDisplayText
    {
        get
        {
            return Projection.InstructorSummary;
        }
    }

    public string LocationDisplayText
    {
        get
        {
            return Projection.LocationSummary;
        }
    }

    public string LogisticsDisplayText
    {
        get
        {
            return InstructorDisplayText + " · " + LocationDisplayText;
        }
    }

    public EOfferingPreference Preference
    {
        get
        {
            return mPreference;
        }
        private set
        {
            if (setProperty(ref mPreference, value))
            {
                raisePropertyChanged(nameof(IsPreferred));
                raisePropertyChanged(nameof(IsAcceptable));
                raisePropertyChanged(nameof(IsExcluded));
                raisePropertyChanged(nameof(PreferenceAccessibleName));
                EventHandler? preferenceChangedOrNull = PreferenceChanged;
                if (preferenceChangedOrNull != null)
                {
                    preferenceChangedOrNull(this, EventArgs.Empty);
                }
            }
        }
    }

    public bool IsPreferred
    {
        get
        {
            return Preference == EOfferingPreference.Preferred;
        }
    }

    public bool IsAcceptable
    {
        get
        {
            return Preference == EOfferingPreference.Acceptable;
        }
    }

    public bool IsExcluded
    {
        get
        {
            return Preference == EOfferingPreference.Excluded;
        }
    }

    public string PreferenceAccessibleName
    {
        get
        {
            return CourseName + ", " + SectionDisplayText + ", " + EnglishInstructionAccessibleText + ", 선택 상태 " + getPreferenceDisplayName(Preference);
        }
    }

    public string PreferredButtonAccessibleName
    {
        get
        {
            return CourseName + ", " + SectionDisplayText + ", 선호";
        }
    }

    public string AcceptableButtonAccessibleName
    {
        get
        {
            return CourseName + ", " + SectionDisplayText + ", 가능";
        }
    }

    public string ExcludedButtonAccessibleName
    {
        get
        {
            return CourseName + ", " + SectionDisplayText + ", 제외";
        }
    }

    public ICommand SelectPreferredCommand { get; }

    public ICommand SelectAcceptableCommand { get; }

    public ICommand SelectExcludedCommand { get; }

    public CourseOfferingPreferenceItem(CatalogCourseProjection courseProjection, CatalogOfferingProjection projection, EOfferingPreference preference)
    {
        if (courseProjection == null)
        {
            throw new ArgumentNullException(nameof(courseProjection));
        }

        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        if (projection.Offering.CourseId != courseProjection.Course.Id)
        {
            throw new ArgumentException("Course offering preferences must belong to the projected course.", nameof(projection));
        }

        if (Enum.IsDefined(typeof(EOfferingPreference), preference) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        CourseProjection = courseProjection;
        Projection = projection;
        mPreference = preference;
        SelectPreferredCommand = new DelegateCommand(selectPreferred);
        SelectAcceptableCommand = new DelegateCommand(selectAcceptable);
        SelectExcludedCommand = new DelegateCommand(selectExcluded);
    }

    public OfferingCandidate CreateCandidate()
    {
        return new OfferingCandidate(OfferingId, Preference);
    }

    private static string getPreferenceDisplayName(EOfferingPreference preference)
    {
        switch (preference)
        {
            case EOfferingPreference.Preferred:
                return "선호";
            case EOfferingPreference.Acceptable:
                return "가능";
            case EOfferingPreference.Excluded:
                return "제외";
            default:
                throw new ArgumentOutOfRangeException(nameof(preference));
        }
    }

    private void selectPreferred()
    {
        if (IsPreferred)
        {
            raisePropertyChanged(nameof(IsPreferred));
            return;
        }

        Preference = EOfferingPreference.Preferred;
    }

    private void selectAcceptable()
    {
        if (IsAcceptable)
        {
            raisePropertyChanged(nameof(IsAcceptable));
            return;
        }

        Preference = EOfferingPreference.Acceptable;
    }

    private void selectExcluded()
    {
        if (IsExcluded)
        {
            raisePropertyChanged(nameof(IsExcluded));
            return;
        }

        Preference = EOfferingPreference.Excluded;
    }
}
