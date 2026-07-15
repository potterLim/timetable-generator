using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace TimetableGenerator;

internal static class Program
{
    private static bool sIsShowingFatalError;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.SetUnhandledExceptionMode(
            UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += onThreadException;
        AppDomain.CurrentDomain.UnhandledException += onUnhandledException;

        try
        {
            System.Windows.Forms.Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            showFatalError(exception);
        }
    }

    private static void onThreadException(
        object? senderOrNull,
        ThreadExceptionEventArgs eventArgs)
    {
        showFatalError(eventArgs.Exception);
    }

    private static void onUnhandledException(
        object? senderOrNull,
        UnhandledExceptionEventArgs eventArgs)
    {
        Exception? exceptionOrNull = eventArgs.ExceptionObject as Exception;
        if (exceptionOrNull == null)
        {
            Trace.TraceError("An unhandled non-exception object terminated the application.");
            return;
        }

        showFatalError(exceptionOrNull);
    }

    private static void showFatalError(Exception exception)
    {
        Trace.TraceError(exception.ToString());
        if (sIsShowingFatalError)
        {
            return;
        }

        sIsShowingFatalError = true;
        try
        {
            MessageBox.Show(
                "예기치 않은 문제가 발생했습니다. 작업 중이던 파일은 변경되지 않았습니다.\r\n\r\n프로그램을 다시 실행한 뒤에도 문제가 계속되면 입력 CSV와 함께 문제를 알려 주세요.",
                "시간표 생성기",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            sIsShowingFatalError = false;
        }
    }
}
