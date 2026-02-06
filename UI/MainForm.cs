using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TimetableGenerator
{
    // App flow: select input CSV -> load/validate -> generate schedules -> export PNGs -> show UI.
    public partial class MainForm : Form
    {
        private static readonly Action<string, string> SHOW_INFO = UiMessageBox.ShowInfo;
        private static readonly Action<string, string> SHOW_WARNING = UiMessageBox.ShowWarning;
        private static readonly Action<string, string> SHOW_ERROR = UiMessageBox.ShowError;

        private List<List<TimeSlot>> mValidSchedules;
        private string mInputFilePath;
        private string mOutputDirectoryPath;

        private ListBox mScheduleListBox;
        private DataGridView mScheduleGridView;
        private Button mOpenSaveFolderButton;

        private readonly ScheduleGridPresenter mGridPresenter;
        private readonly SchedulePngExporter mPngExporter;

        private bool mHasInitialized;

        public MainForm()
        {
            InitializeComponent();

            mGridPresenter = new ScheduleGridPresenter();
            mPngExporter = new SchedulePngExporter(mGridPresenter);

            Text = "시간표 생성기";
            Width = UiConstants.FORM_WIDTH;
            Height = UiConstants.FORM_HEIGHT;

            MinimumSize = MinimumSizeCalculator.CalculateFormMinimumSize();

            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiConstants.MAIN_BACKGROUND_COLOR;

            Shown += onMainFormShown;
        }

        private void onMainFormShown(object sender, EventArgs e)
        {
            if (mHasInitialized)
            {
                return;
            }

            mHasInitialized = true;

            string inputFilePath;
            if (!trySelectInputFilePath(out inputFilePath))
            {
                requestCloseApplication();
                return;
            }

            List<Course> courses;
            string errorMessage;

            DataLoader loader = new DataLoader();
            if (!loader.TryLoadCoursesFromCsv(inputFilePath, out courses, out errorMessage))
            {
                showAndClose(SHOW_WARNING, errorMessage, "입력 오류");
                return;
            }

            List<List<TimeSlot>> schedules;
            if (!ScheduleGenerator.TryGenerateValidSchedules(courses, out schedules, out errorMessage))
            {
                showAndClose(SHOW_WARNING, errorMessage, "데이터 오류");
                return;
            }

            if (schedules == null || schedules.Count == 0)
            {
                showAndClose(SHOW_WARNING, "유효한 시간표가 없습니다.", "시간표 생성 실패");
                return;
            }

            // Export all schedules first so the user can immediately access results even if UI is closed.
            string outputDir;
            if (!mPngExporter.TrySaveAll(schedules, inputFilePath, out outputDir, out errorMessage))
            {
                showAndClose(SHOW_ERROR, errorMessage, "저장 오류");
                return;
            }

            mValidSchedules = schedules;
            mInputFilePath = inputFilePath;
            mOutputDirectoryPath = outputDir;

            initializeUi();

            mOpenSaveFolderButton.Enabled = Directory.Exists(mOutputDirectoryPath);
            mScheduleListBox.SelectedIndex = 0;
        }

        private void showAndClose(Action<string, string> show, string message, string title)
        {
            if (show != null)
            {
                show(message, title);
            }

            requestCloseApplication();
        }

        private void requestCloseApplication()
        {
            BeginInvoke((MethodInvoker)delegate { Close(); });
        }

        private bool trySelectInputFilePath(out string inputFilePath)
        {
            inputFilePath = null;

            string inputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input");
            if (!Directory.Exists(inputDir))
            {
                Directory.CreateDirectory(inputDir);

                // First-run behavior: create "input" folder and ask the user to re-run after placing a CSV file.
                SHOW_INFO(buildInputFolderCreatedMessage(), "입력 폴더 생성");
                return false;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.InitialDirectory = inputDir;
                dialog.Filter = "CSV 파일 (*.csv)|*.csv";
                dialog.Title = "시간표 생성용 CSV 파일 선택";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    SHOW_INFO("파일 선택을 취소했습니다.", "파일 선택");
                    return false;
                }

                inputFilePath = dialog.FileName;
            }

            if (!File.Exists(inputFilePath))
            {
                SHOW_ERROR(UiMessageBox.BuildFilePathMessage("선택한 파일을 찾을 수 없습니다.", inputFilePath), "파일 오류");
                return false;
            }

            return true;
        }

        private static string buildInputFolderCreatedMessage()
        {
            return UiMessageBox.BuildParagraphMessage("입력 폴더가 없어 생성했습니다.", "input 폴더에 CSV 파일을 넣은 뒤 다시 실행하세요.");
        }

        private void initializeUi()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 1;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiConstants.LEFT_PANEL_WIDTH));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Control leftPanel = createLeftPanel();
            mScheduleGridView = mGridPresenter.CreateGrid();

            mainLayout.Controls.Add(leftPanel, 0, 0);
            mainLayout.Controls.Add(mScheduleGridView, 1, 0);

            Controls.Add(mainLayout);

            mScheduleGridView.DataBindingComplete += onGridDataBindingComplete;
            mScheduleGridView.SizeChanged += onScheduleGridSizeChanged;
            mScheduleListBox.SelectedIndexChanged += onScheduleSelected;
        }

        private void onScheduleGridSizeChanged(object sender, EventArgs e)
        {
            mGridPresenter.ApplySizingAndHeaderStyle(mScheduleGridView);
        }

        private Control createLeftPanel()
        {
            TableLayoutPanel leftLayout = new TableLayoutPanel();
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.Padding = new Padding(UiConstants.LEFT_PANEL_PADDING);
            leftLayout.Margin = Padding.Empty;
            leftLayout.ColumnCount = 1;
            leftLayout.RowCount = 4;

            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiConstants.LIST_HEIGHT));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiConstants.LEFT_GAP_HEIGHT));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, UiConstants.LEFT_ACTION_ROW_HEIGHT));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            mScheduleListBox = new ListBox();
            mScheduleListBox.Dock = DockStyle.Fill;
            mScheduleListBox.Font = UiConstants.DEFAULT_FONT;
            mScheduleListBox.ItemHeight = UiConstants.LIST_ITEM_HEIGHT;
            mScheduleListBox.IntegralHeight = false;

            for (int i = 0; i < mValidSchedules.Count; ++i)
            {
                int scheduleNumber = i + 1;
                mScheduleListBox.Items.Add("시간표 " + scheduleNumber);
            }

            mOpenSaveFolderButton = new Button();
            mOpenSaveFolderButton.Text = "저장 위치 열기";
            mOpenSaveFolderButton.Font = UiConstants.DEFAULT_FONT;
            mOpenSaveFolderButton.FlatStyle = FlatStyle.System;
            mOpenSaveFolderButton.Enabled = false;
            mOpenSaveFolderButton.Click += onOpenSaveFolderClicked;

            mOpenSaveFolderButton.Dock = DockStyle.None;
            mOpenSaveFolderButton.Width = UiConstants.LEFT_BUTTON_WIDTH;
            mOpenSaveFolderButton.Height = UiConstants.LEFT_BUTTON_HEIGHT;
            mOpenSaveFolderButton.Margin = Padding.Empty;
            mOpenSaveFolderButton.Padding = Padding.Empty;
            mOpenSaveFolderButton.Anchor = AnchorStyles.None;

            Panel buttonHost = new Panel();
            buttonHost.Dock = DockStyle.Fill;
            buttonHost.Margin = Padding.Empty;
            buttonHost.Padding = Padding.Empty;

            buttonHost.Controls.Add(mOpenSaveFolderButton);
            buttonHost.Resize += onButtonHostResized;

            centerButtonInHost(buttonHost, mOpenSaveFolderButton);

            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Fill;
            spacer.Margin = Padding.Empty;
            spacer.Padding = Padding.Empty;

            leftLayout.Controls.Add(mScheduleListBox, 0, 0);
            leftLayout.Controls.Add(buttonHost, 0, 2);
            leftLayout.Controls.Add(spacer, 0, 3);

            return leftLayout;
        }

        private void onButtonHostResized(object sender, EventArgs e)
        {
            Panel host = sender as Panel;
            if (host == null)
            {
                return;
            }

            centerButtonInHost(host, mOpenSaveFolderButton);
        }

        private void centerButtonInHost(Panel host, Button button)
        {
            if (host == null || button == null)
            {
                return;
            }

            int x = (host.ClientSize.Width - button.Width) / 2;
            int y = (host.ClientSize.Height - button.Height) / 2;

            if (x < 0)
            {
                x = 0;
            }

            if (y < 0)
            {
                y = 0;
            }

            button.Location = new Point(x, y);
        }

        private void onScheduleSelected(object sender, EventArgs e)
        {
            if (mScheduleListBox.SelectedIndex < 0)
            {
                return;
            }

            int index = mScheduleListBox.SelectedIndex;

            DataTable table;
            string errorMessage;

            if (!ScheduleGenerator.TryGenerateTable(mValidSchedules[index], out table, out errorMessage))
            {
                showAndClose(SHOW_ERROR, errorMessage, "데이터 오류");
                return;
            }

            mScheduleGridView.DataSource = table;
        }

        private void onGridDataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            mGridPresenter.ApplySizingAndHeaderStyle(mScheduleGridView);
        }

        private void onOpenSaveFolderClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(mOutputDirectoryPath))
            {
                return;
            }

            if (!Directory.Exists(mOutputDirectoryPath))
            {
                SHOW_WARNING(UiMessageBox.BuildLocationMessage("저장 위치를 찾을 수 없습니다.", mOutputDirectoryPath), "저장 위치");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", mOutputDirectoryPath) { UseShellExecute = true });
            }
            catch
            {
                SHOW_ERROR(UiMessageBox.BuildLocationMessage("저장 위치를 여는 데 실패했습니다.", mOutputDirectoryPath), "오류");
            }
        }
    }
}
