using System;
using System.Windows.Forms;

namespace TimetableGenerator
{
    internal static class Program
    {
        /// <summary>
        /// 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                // 프로그램 실행 중 발생한 알 수 없는 오류를 사용자에게 알림
                MessageBox.Show(
                    $"프로그램 실행 중 알 수 없는 오류가 발생했습니다.\n\n오류 메시지: {ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }
    }
}