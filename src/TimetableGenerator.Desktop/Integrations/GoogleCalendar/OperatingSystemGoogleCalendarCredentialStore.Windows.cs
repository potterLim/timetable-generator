using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed partial class OperatingSystemGoogleCalendarCredentialStore
{
    private const uint WINDOWS_CREDENTIAL_TYPE_GENERIC = 1U;
    private const uint WINDOWS_CREDENTIAL_PERSIST_LOCAL_MACHINE = 2U;

    private static string createWindowsTargetName(string accountName)
    {
        return CREDENTIAL_SERVICE_NAME + "." + accountName;
    }

    private static GoogleRefreshToken? readWindowsCredentialOrNull(string accountName)
    {
        string targetName = createWindowsTargetName(accountName);
        IntPtr credentialPointer;
        if (credRead(
            targetName,
            WINDOWS_CREDENTIAL_TYPE_GENERIC,
            0U,
            out credentialPointer) == false)
        {
            int errorCode = Marshal.GetLastWin32Error();
            if (errorCode == 1168)
            {
                return null;
            }

            throw new Win32Exception(errorCode);
        }

        try
        {
            SWindowsNativeCredential credential =
                Marshal.PtrToStructure<SWindowsNativeCredential>(credentialPointer);
            byte[] tokenBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, tokenBytes, 0, tokenBytes.Length);
            try
            {
                return new GoogleRefreshToken(Encoding.UTF8.GetString(tokenBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(tokenBytes);
            }
        }
        finally
        {
            credFree(credentialPointer);
        }
    }

    private static void saveWindowsCredential(
        string accountName,
        GoogleRefreshToken refreshToken)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(refreshToken.Value);
        IntPtr tokenPointer = IntPtr.Zero;
        try
        {
            tokenPointer = Marshal.AllocCoTaskMem(tokenBytes.Length);
            Marshal.Copy(tokenBytes, 0, tokenPointer, tokenBytes.Length);
            SWindowsNativeCredential credential = new SWindowsNativeCredential(
                WINDOWS_CREDENTIAL_TYPE_GENERIC,
                createWindowsTargetName(accountName),
                checked((uint)tokenBytes.Length),
                tokenPointer,
                WINDOWS_CREDENTIAL_PERSIST_LOCAL_MACHINE,
                accountName);

            if (credWrite(ref credential, 0U) == false)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
            if (tokenPointer != IntPtr.Zero)
            {
                try
                {
                    Marshal.Copy(tokenBytes, 0, tokenPointer, tokenBytes.Length);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(tokenPointer);
                }
            }
        }
    }

    private static void deleteWindowsCredential(string accountName)
    {
        if (credDelete(
            createWindowsTargetName(accountName),
            WINDOWS_CREDENTIAL_TYPE_GENERIC,
            0U))
        {
            return;
        }

        int errorCode = Marshal.GetLastWin32Error();
        if (errorCode != 1168)
        {
            throw new Win32Exception(errorCode);
        }
    }

    [DllImport(
        "Advapi32.dll",
        EntryPoint = "CredReadW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool credRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credential);

    [DllImport(
        "Advapi32.dll",
        EntryPoint = "CredWriteW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool credWrite(
        ref SWindowsNativeCredential credential,
        uint flags);

    [DllImport(
        "Advapi32.dll",
        EntryPoint = "CredDeleteW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool credDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", ExactSpelling = true)]
    private static extern void credFree(IntPtr buffer);
}
