using System;
using System.Windows.Forms;

namespace TimetableGenerator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                UiMessageBox.ShowError(UiMessageBox.BuildErrorMessage("프로그램을 실행하는 동안 문제가 발생했습니다.", ex.Message), "오류");
                Application.Exit();
            }
        }
    }
}
