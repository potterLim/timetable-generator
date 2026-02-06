using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TimetableGenerator
{
    // Exports schedules as PNG by rendering an offscreen, borderless Form containing a DataGridView.
    // Note: DrawToBitmap depends on WinForms rendering and may vary with DPI/theme.
    // The temp form is positioned offscreen and fully transparent to avoid visible flicker.
    public sealed class SchedulePngExporter
    {
        private readonly ScheduleGridPresenter mGridPresenter;

        public SchedulePngExporter(ScheduleGridPresenter gridPresenter)
        {
            mGridPresenter = gridPresenter;
        }

        public bool TrySaveAll(
            List<List<TimeSlot>> schedules,
            string inputFilePath,
            out string outputDirectoryPath,
            out string errorMessage)
        {
            outputDirectoryPath = null;
            errorMessage = null;

            if (schedules == null || schedules.Count == 0)
            {
                errorMessage = "유효한 시간표가 없습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                errorMessage = "입력 파일 경로가 비어 있습니다.";
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(inputFilePath);
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", baseName);

            try
            {
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }
            catch
            {
                errorMessage = UiMessageBox.BuildLocationMessage("출력 폴더를 생성하지 못했습니다.", outputDir);
                return false;
            }

            for (int i = 0; i < schedules.Count; ++i)
            {
                int scheduleNumber = i + 1;
                string fileName = baseName + "_시간표" + scheduleNumber + ".png";
                string filePath = Path.Combine(outputDir, fileName);

                if (!TrySaveScheduleAsPng(schedules[i], filePath, out errorMessage))
                {
                    return false;
                }

                // Process pending UI messages to keep the app responsive during batch export.
                Application.DoEvents();
            }

            outputDirectoryPath = outputDir;
            return true;
        }

        public bool TrySaveScheduleAsPng(List<TimeSlot> schedule, string filePath, out string errorMessage)
        {
            errorMessage = null;

            DataTable table;
            if (!ScheduleGenerator.TryGenerateTable(schedule, out table, out errorMessage))
            {
                return false;
            }

            try
            {
                using (Form tempForm = new Form())
                {
                    // Temporary offscreen form used only for rendering.
                    tempForm.FormBorderStyle = FormBorderStyle.None;
                    tempForm.StartPosition = FormStartPosition.Manual;
                    tempForm.Location = new Point(UiConstants.OFFSCREEN_X, UiConstants.OFFSCREEN_Y);
                    tempForm.Size = new Size(UiConstants.FORM_WIDTH, UiConstants.FORM_HEIGHT);
                    tempForm.ShowInTaskbar = false;
                    tempForm.TopMost = false;
                    tempForm.Opacity = 0.0;
                    tempForm.ControlBox = false;

                    DataGridView tempGrid = mGridPresenter.CreateGrid();
                    tempGrid.DataBindingComplete += onTempGridDataBindingComplete;
                    tempGrid.DataSource = table;

                    tempForm.Controls.Add(tempGrid);

                    tempForm.Show();
                    Application.DoEvents();

                    tempForm.Refresh();
                    tempGrid.Refresh();
                    Application.DoEvents();

                    using (Bitmap bitmap = new Bitmap(tempForm.Width, tempForm.Height))
                    {
                        tempForm.DrawToBitmap(bitmap, new Rectangle(0, 0, tempForm.Width, tempForm.Height));
                        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    tempForm.Hide();
                }
            }
            catch
            {
                errorMessage = UiMessageBox.BuildFilePathMessage("시간표 이미지를 저장하지 못했습니다.", filePath);
                return false;
            }

            return true;
        }

        private void onTempGridDataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null)
            {
                return;
            }

            mGridPresenter.ApplySizingAndHeaderStyle(grid);
        }
    }
}
