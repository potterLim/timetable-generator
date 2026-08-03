using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private static readonly PresentationScheduleRecommendation EMPTY_RECOMMENDATION = new PresentationScheduleRecommendation(Array.Empty<ScheduleEntry>());

    private readonly IScheduleRecommendationProvider mRecommendationProvider;

    private readonly DelegateCommand mPreviousRecommendationCommand;

    private readonly DelegateCommand mNextRecommendationCommand;

    private readonly DelegateCommand mRetryRecommendationCommand;

    private IReadOnlyList<ScheduleRecommendationViewItem> mRecommendations;

    private IReadOnlyList<PresentationScheduleRecommendation> mPngExportCandidateSchedules;

    private PresentationScheduleRecommendation mPersonalSchedulePreview;

    private ScheduleBoardDayRange mRecommendationDayRange;

    private int mRecommendationIndex;

    private CancellationTokenSource mRecommendationCancellationSource;

    private Task mRecommendationRefreshTask;

    private ERecommendationCalculationState mRecommendationCalculationState;

    private string mRecommendationCalculationError;

    private bool mHasUnsatisfiedScheduleConstraints;

    public PresentationScheduleRecommendation ActiveRecommendation
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return EMPTY_RECOMMENDATION;
            }

            return mRecommendations[mRecommendationIndex].Schedule;
        }
    }

    public PresentationScheduleRecommendation DisplayedSchedule
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return mPersonalSchedulePreview;
            }

            return ActiveRecommendation;
        }
    }

    public ScheduleBoardPresentation? DisplayedScheduleBoard
    {
        get
        {
            PlanTabItem? activePlanOrNull = mActivePlanOrNull;
            if (activePlanOrNull == null)
            {
                return null;
            }

            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            return new ScheduleBoardPresentation(
                DisplayedSchedule,
                createScheduleBoardLayout(),
                activePlanOrNull.Name,
                catalog.InstitutionName,
                catalog.Term);
        }
    }

    internal IReadOnlyList<ScheduleBoardPresentation> PngExportCandidates
    {
        get
        {
            PlanTabItem? activePlanOrNull = mActivePlanOrNull;
            if (activePlanOrNull == null || mRecommendations.Count == 0)
            {
                return Array.Empty<ScheduleBoardPresentation>();
            }

            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            List<ScheduleBoardPresentation> candidates = new List<ScheduleBoardPresentation>(mPngExportCandidateSchedules.Count);
            foreach (PresentationScheduleRecommendation schedule in mPngExportCandidateSchedules)
            {
                candidates.Add(new ScheduleBoardPresentation(schedule, activePlanOrNull.Name, catalog.InstitutionName, catalog.Term));
            }

            return candidates.AsReadOnly();
        }
    }

    public bool CanExportAllPngCandidates
    {
        get
        {
            return mRecommendationExpansionState == ERecommendationExpansionState.Unavailable
                && mPngExportCandidateSchedules.Count > 1;
        }
    }

    public string RecommendationSummary
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return "0 / 0";
            }

            string additionalRecommendationIndicator = "+";
            if (mRecommendationExpansionState == ERecommendationExpansionState.Unavailable)
            {
                additionalRecommendationIndicator = string.Empty;
            }
            return (mRecommendationIndex + 1) + " / " + mRecommendations.Count + additionalRecommendationIndicator;
        }
    }

    public bool HasRecommendations
    {
        get
        {
            return mRecommendations.Count > 0;
        }
    }

    public bool HasMultipleRecommendations
    {
        get
        {
            return mRecommendations.Count > 1;
        }
    }

    public bool HasScheduleEntries
    {
        get
        {
            return DisplayedSchedule.Entries.Count > 0;
        }
    }

    public bool HasUnsatisfiedScheduleConstraints
    {
        get
        {
            return mHasUnsatisfiedScheduleConstraints;
        }
    }

    public bool CanExportSchedule
    {
        get
        {
            return HasActivePlan
                && HasScheduleEntries
                && HasUnsatisfiedScheduleConstraints == false;
        }
    }

    public bool IsScheduleEmpty
    {
        get
        {
            return HasActivePlan
                && HasScheduleEntries == false
                && HasUnsatisfiedScheduleConstraints == false
                && IsRecommendationCalculating == false
                && HasRecommendationCalculationError == false;
        }
    }

    public bool IsUnsatisfiedScheduleEmpty
    {
        get
        {
            return HasUnsatisfiedScheduleConstraints && HasScheduleEntries == false;
        }
    }

    public bool HasUnsatisfiedPersonalSchedulePreview
    {
        get
        {
            return HasUnsatisfiedScheduleConstraints && HasScheduleEntries;
        }
    }

    public bool IsRecommendationCalculating
    {
        get
        {
            return mRecommendationCalculationState == ERecommendationCalculationState.Calculating;
        }
    }

    public bool HasRecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationState == ERecommendationCalculationState.Failed;
        }
    }

    public string RecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationError;
        }
    }

    public string EmptyScheduleTitle
    {
        get
        {
            if (HasActivePlan == false)
            {
                return string.Empty;
            }

            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 선택해 시간표를 구성해 보세요";
            }

            if (HasRecommendations && HasScheduleEntries == false)
            {
                return "시간이 정해진 과목이 없습니다";
            }

            return "겹치지 않는 시간표 조합을 찾지 못했습니다";
        }
    }

    public string EmptyScheduleMessage
    {
        get
        {
            if (HasActivePlan == false)
            {
                return string.Empty;
            }

            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 선택하면 가능한 시간표를 자동으로 만듭니다.";
            }

            if (HasRecommendations && HasScheduleEntries == false)
            {
                return "시간 미정 과목은 현재 시간표에 유지됩니다.";
            }

            return "겹치는 과목을 빼거나 분반 선호를 바꾸세요.";
        }
    }

    public ICommand PreviousRecommendationCommand
    {
        get
        {
            return mPreviousRecommendationCommand;
        }
    }

    public ICommand NextRecommendationCommand
    {
        get
        {
            return mNextRecommendationCommand;
        }
    }

    public ICommand RetryRecommendationCommand
    {
        get
        {
            return mRetryRecommendationCommand;
        }
    }

    internal Task RecommendationRefreshTask
    {
        get
        {
            return mRecommendationRefreshTask;
        }
    }

    private void notifyRecommendationCalculationStateChanged()
    {
        raisePropertyChanged(nameof(IsRecommendationCalculating));
        raisePropertyChanged(nameof(HasRecommendationCalculationError));
        raisePropertyChanged(nameof(RecommendationCalculationError));
        raisePropertyChanged(nameof(HasUnsatisfiedScheduleConstraints));
        raisePropertyChanged(nameof(CanExportSchedule));
        raisePropertyChanged(nameof(IsUnsatisfiedScheduleEmpty));
        raisePropertyChanged(nameof(HasUnsatisfiedPersonalSchedulePreview));
        mRetryRecommendationCommand.NotifyCanExecuteChanged();
    }

    private void notifyRecommendationChanged()
    {
        synchronizeDisplayedTimeNotProvidedSelections();
        raisePropertyChanged(nameof(ActiveRecommendation));
        raisePropertyChanged(nameof(DisplayedSchedule));
        raisePropertyChanged(nameof(DisplayedScheduleBoard));
        raisePropertyChanged(nameof(RecommendationSummary));
        raisePropertyChanged(nameof(HasRecommendations));
        raisePropertyChanged(nameof(HasMultipleRecommendations));
        raisePropertyChanged(nameof(CanExportAllPngCandidates));
        raisePropertyChanged(nameof(HasUnsatisfiedScheduleConstraints));
        raisePropertyChanged(nameof(CanExportSchedule));
        raisePropertyChanged(nameof(HasScheduleEntries));
        raisePropertyChanged(nameof(IsScheduleEmpty));
        raisePropertyChanged(nameof(IsUnsatisfiedScheduleEmpty));
        raisePropertyChanged(nameof(HasUnsatisfiedPersonalSchedulePreview));
        raisePropertyChanged(nameof(EmptyScheduleTitle));
        raisePropertyChanged(nameof(EmptyScheduleMessage));
        mPreviousRecommendationCommand.NotifyCanExecuteChanged();
        mNextRecommendationCommand.NotifyCanExecuteChanged();
    }

    private void synchronizeDisplayedTimeNotProvidedSelections()
    {
        ScheduleRecommendationBookmark? recommendationBookmarkOrNull = null;
        if (mRecommendations.Count > 0)
        {
            recommendationBookmarkOrNull = mRecommendations[mRecommendationIndex].BookmarkOrNull;
        }

        PlanTabItem? activePlanOrNull = mActivePlanOrNull;
        if (activePlanOrNull == null)
        {
            return;
        }

        foreach (PlanCourseChoiceGroupItem group in activePlanOrNull.CourseChoiceGroups)
        {
            group.SynchronizeSelectedOfferings(recommendationBookmarkOrNull);
        }
    }
}
