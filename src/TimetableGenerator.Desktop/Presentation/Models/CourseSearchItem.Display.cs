using TimetableGenerator.Desktop.Presentation.Catalog;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed partial class CourseSearchItem
{
    public string InstructorDisplayText { get; }

    public string CreditDisplayText
    {
        get
        {
            return Credits + "학점";
        }
    }

    public string InstructorCreditDisplayText
    {
        get
        {
            return InstructorDisplayText + " · " + CreditDisplayText;
        }
    }

    public string CourseBrowserMetadataDisplayText
    {
        get
        {
            if (HasSingleOfferingDetails)
            {
                return InstructorCreditDisplayText + " · " + SelectedSelectionOption.EnglishInstructionDisplayText;
            }

            return InstructorCreditDisplayText;
        }
    }

    public string EnglishInstructionAccessibleText
    {
        get
        {
            return SelectedSelectionOption.EnglishInstructionAccessibleText;
        }
    }

    public string CourseBrowserAccessibleName
    {
        get
        {
            string accessibleName = Code + ", " + Name + ", " + InstructorDisplayText + ", " + CreditDisplayText;
            if (HasSingleOfferingDetails)
            {
                return accessibleName + ", " + EnglishInstructionAccessibleText;
            }

            return accessibleName;
        }
    }

    public string SingleOfferingDetailsDisplayText { get; }

    public bool HasSingleOfferingDetails
    {
        get
        {
            return Projection.Offerings.Count == 1;
        }
    }

    public ECourseAccent Accent
    {
        get
        {
            return Projection.Accent;
        }
    }

    public bool HasMultipleSelectionOptions
    {
        get
        {
            return SelectionOptions.Count > 1;
        }
    }

    public string SelectionAccessibleName
    {
        get
        {
            return Name + ", 추가할 분반 선택";
        }
    }

    public bool IsBlue
    {
        get
        {
            return Accent == ECourseAccent.Blue;
        }
    }

    public bool IsPurple
    {
        get
        {
            return Accent == ECourseAccent.Purple;
        }
    }

    public bool IsGreen
    {
        get
        {
            return Accent == ECourseAccent.Green;
        }
    }

    public bool IsDirectAddButtonVisible
    {
        get
        {
            return IsAdded == false;
        }
    }

    public bool IsSelectionEnabled
    {
        get
        {
            return IsAdded == false;
        }
    }

    public string AddButtonAccessibleName
    {
        get
        {
            if (IsAdded)
            {
                return Name + "은 현재 시간표에 추가되어 있습니다.";
            }

            if (Projection.Offerings.Count > 1)
            {
                return Name + " 수강 선택 설정 열기";
            }

            if (SelectedSelectionOption.IsDirectAdd)
            {
                return Name + ", " + SelectedSelectionOption.AccessibleName + ", 현재 시간표에 추가";
            }

            if (ScheduledOfferingCount > 1)
            {
                return Name + "의 분반 선호 설정 열기";
            }

            return Name + "을 현재 시간표에 추가";
        }
    }

    public string AddButtonHelpText
    {
        get
        {
            if (Projection.Offerings.Count > 1)
            {
                return "분반별 선호를 설정합니다.";
            }

            if (SelectedSelectionOption.IsDirectAdd)
            {
                return "선택한 분반: " + SelectedSelectionOption.AccessibleName;
            }

            return "분반별 선호를 설정합니다.";
        }
    }

    public string AddButtonToolTipText
    {
        get
        {
            if (Projection.Offerings.Count > 1)
            {
                return "수강 선택 설정";
            }

            if (SelectedSelectionOption.IsDirectAdd)
            {
                return SelectedSelectionOption.DisplayName + " 추가";
            }

            if (ScheduledOfferingCount > 1)
            {
                return "분반 선호 설정";
            }

            return "시간표에 추가";
        }
    }

    public string SelectedCourseActionAccessibleName
    {
        get
        {
            return mCourseSelectionAction switch
            {
                ECourseSelectionAction.Remove => Name + "을 시간표에서 제거",
                ECourseSelectionAction.Edit => Name + " 수강 선택 수정",
                _ => Name + "은 현재 시간표에 추가되어 있지 않습니다.",
            };
        }
    }

    public string SelectedCourseActionToolTipText
    {
        get
        {
            return mCourseSelectionAction switch
            {
                ECourseSelectionAction.Remove => "시간표에서 제거",
                ECourseSelectionAction.Edit => "수강 선택 수정",
                _ => string.Empty,
            };
        }
    }

    private static string createInstructorSummary(CatalogCourseProjection projection)
    {
        if (projection.Offerings.Count == 1)
        {
            return projection.Offerings[0].InstructorSummary;
        }

        return projection.Offerings.Count + "개 분반";
    }

    private static string createSingleOfferingDetails(CatalogCourseProjection projection)
    {
        if (projection.Offerings.Count != 1)
        {
            return string.Empty;
        }

        CatalogOfferingProjection offering = projection.Offerings[0];
        return offering.ScheduleSummary + " · " + offering.LocationSummary;
    }
}
