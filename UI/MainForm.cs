using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using TimetableGenerator.Application.Documents;
using TimetableGenerator.Infrastructure.Exporting;
using TimetableGenerator.UI.Product;
using ProductSchedulePngExporter = TimetableGenerator.Infrastructure.Exporting.SchedulePngExporter;

namespace TimetableGenerator;

internal sealed partial class MainForm : Form
{
    private const int INITIAL_CLIENT_WIDTH = 1_280;
    private const int INITIAL_CLIENT_HEIGHT = 780;
    private const int MINIMUM_WINDOW_WIDTH = 960;
    private const int MINIMUM_WINDOW_HEIGHT = 600;
    private const int WORKING_AREA_MARGIN = 32;
    private const int MAXIMUM_VISIBLE_EXPORT_FAILURE_COUNT = 3;

    private readonly ScheduleDocumentLoader mDocumentLoader;
    private readonly ProductSchedulePngExporter mPngExporter;

    private readonly TableLayoutPanel mShellLayout;
    private readonly AppHeaderControl mHeaderControl;
    private readonly Panel mContentHost;
    private readonly AppStatusControl mStatusControl;
    private readonly WelcomeControl mWelcomeControl;
    private readonly LoadingControl mLoadingControl;
    private readonly MessageStateControl mMessageStateControl;
    private readonly ReadyScheduleControl mReadyScheduleControl;

    private ScheduleDocument? mDocumentOrNull;
    private CancellationTokenSource? mOperationCancellationOrNull;
    private ScheduleExportDirectoryPath mPendingExportDirectory;
    private ScheduleExportDirectoryPath mLastExportDirectory;
    private ScheduleIndex mSelectedScheduleIndex;
    private EAppViewState mViewState;
    private EAppOperation mOperation;
    private bool mShouldRestoreDocumentFromMessageAction;
    private bool mShouldCloseAfterOperation;

    internal MainForm()
    {
        mDocumentLoader = new ScheduleDocumentLoader();
        mPngExporter = new ProductSchedulePngExporter();

        AutoScaleDimensions = new SizeF(DesignTokens.BASE_DPI, DesignTokens.BASE_DPI);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        ClientSize = findInitialClientSize();
        KeyPreview = true;
        MinimumSize = new Size(MINIMUM_WINDOW_WIDTH, MINIMUM_WINDOW_HEIGHT);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "시간표 생성기";

        mHeaderControl = new AppHeaderControl();
        mHeaderControl.Dock = DockStyle.Fill;
        mHeaderControl.CsvOpenRequested += onCsvOpenRequested;
        mHeaderControl.PngExportRequested += onPngExportRequested;
        mHeaderControl.OutputFolderOpenRequested += onOutputFolderOpenRequested;

        mStatusControl = new AppStatusControl();
        mStatusControl.Dock = DockStyle.Fill;

        mWelcomeControl = new WelcomeControl();
        mWelcomeControl.Dock = DockStyle.Fill;
        mWelcomeControl.CsvOpenRequested += onCsvOpenRequested;
        mWelcomeControl.CsvFileDropped += onCsvFileDropped;
        mWelcomeControl.ExampleFormatRequested += onExampleFormatRequested;

        mLoadingControl = new LoadingControl();
        mLoadingControl.Dock = DockStyle.Fill;
        mLoadingControl.CancelRequested += onCancelRequested;

        mMessageStateControl = new MessageStateControl();
        mMessageStateControl.Dock = DockStyle.Fill;
        mMessageStateControl.PrimaryActionRequested += onMessagePrimaryActionRequested;

        mReadyScheduleControl = new ReadyScheduleControl();
        mReadyScheduleControl.Dock = DockStyle.Fill;
        mReadyScheduleControl.SelectedScheduleChanged += onSelectedScheduleChanged;

        mContentHost = createContentHost();
        mShellLayout = createShellLayout();
        mShellLayout.Controls.Add(mHeaderControl, 0, 0);
        mShellLayout.Controls.Add(mContentHost, 0, 1);
        mShellLayout.Controls.Add(mStatusControl, 0, 2);
        Controls.Add(mShellLayout);

        mOperation = EAppOperation.None;
        mSelectedScheduleIndex = new ScheduleIndex(0);
        showWelcomeView();
        applyShellMetrics();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O))
        {
            requestCsvOpen();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.E))
        {
            requestScheduleExport(EScheduleExportScope.AllSchedules);
            return true;
        }

        if (keyData == (Keys.Control | Keys.E))
        {
            requestScheduleExport(EScheduleExportScope.CurrentSchedule);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Left))
        {
            selectPreviousSchedule();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Right))
        {
            selectNextSchedule();
            return true;
        }

        if (keyData == Keys.Escape && mOperation != EAppOperation.None)
        {
            cancelCurrentOperation();
            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs eventArgs)
    {
        base.OnDpiChanged(eventArgs);
        applyShellMetrics();
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        base.OnFormClosing(eventArgs);
        if (eventArgs.Cancel || mOperation == EAppOperation.None)
        {
            return;
        }

        eventArgs.Cancel = true;
        mShouldCloseAfterOperation = true;
        cancelCurrentOperation();
    }

    private Panel createContentHost()
    {
        Panel contentHost = new Panel();
        contentHost.Dock = DockStyle.Fill;
        contentHost.Margin = Padding.Empty;
        contentHost.Padding = Padding.Empty;
        contentHost.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        contentHost.Controls.Add(mWelcomeControl);
        contentHost.Controls.Add(mLoadingControl);
        contentHost.Controls.Add(mMessageStateControl);
        contentHost.Controls.Add(mReadyScheduleControl);
        return contentHost;
    }

    private Size findInitialClientSize()
    {
        Rectangle workingArea = SystemInformation.WorkingArea;
        int maximumWindowWidth = Math.Max(
            MINIMUM_WINDOW_WIDTH,
            workingArea.Width - WORKING_AREA_MARGIN);
        int maximumWindowHeight = Math.Max(
            MINIMUM_WINDOW_HEIGHT,
            workingArea.Height - WORKING_AREA_MARGIN);
        Size preferredClientSize = new Size(
            INITIAL_CLIENT_WIDTH,
            INITIAL_CLIENT_HEIGHT);
        Size preferredWindowSize = SizeFromClientSize(preferredClientSize);
        int nonClientWidth = preferredWindowSize.Width - preferredClientSize.Width;
        int nonClientHeight = preferredWindowSize.Height - preferredClientSize.Height;
        int maximumClientWidth = Math.Max(
            1,
            maximumWindowWidth - nonClientWidth);
        int maximumClientHeight = Math.Max(
            1,
            maximumWindowHeight - nonClientHeight);
        return new Size(
            Math.Min(INITIAL_CLIENT_WIDTH, maximumClientWidth),
            Math.Min(INITIAL_CLIENT_HEIGHT, maximumClientHeight));
    }

    private TableLayoutPanel createShellLayout()
    {
        TableLayoutPanel shellLayout = new TableLayoutPanel();
        shellLayout.ColumnCount = 1;
        shellLayout.RowCount = 3;
        shellLayout.Dock = DockStyle.Fill;
        shellLayout.Margin = Padding.Empty;
        shellLayout.Padding = Padding.Empty;
        shellLayout.BackColor = DesignTokens.WINDOW_BACKGROUND_COLOR;
        shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100.0f));
        shellLayout.RowStyles.Add(new RowStyle(
            SizeType.Absolute,
            DesignTokens.APP_HEADER_HEIGHT));
        shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100.0f));
        shellLayout.RowStyles.Add(new RowStyle(
            SizeType.Absolute,
            DesignTokens.APP_STATUS_HEIGHT));
        return shellLayout;
    }
}
