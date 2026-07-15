using System;
using System.Windows.Forms;

namespace TimetableGenerator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            string errorMessage = UiMessageBox.BuildErrorMessage(
                "프로그램을 실행하는 동안 문제가 발생했습니다.",
                exception.Message);

            UiMessageBox.ShowError(errorMessage, "오류");
            Application.Exit();
        }
    }
}
