using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class OperatingSystemGoogleCalendarCredentialStore
    : IGoogleCalendarCredentialStore
{
    private const string CREDENTIAL_SERVICE_NAME = "TimetableGenerator.GoogleCalendar";
    private const uint WINDOWS_CREDENTIAL_TYPE_GENERIC = 1U;
    private const uint WINDOWS_CREDENTIAL_PERSIST_LOCAL_MACHINE = 2U;
    private const int MACOS_ITEM_NOT_FOUND_STATUS = -25300;

    public Task<GoogleRefreshToken?> ReadRefreshTokenOrNullAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string accountName = createAccountName(clientId);
        GoogleRefreshToken? refreshTokenOrNull;
        if (OperatingSystem.IsWindows())
        {
            refreshTokenOrNull = readWindowsCredentialOrNull(accountName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            refreshTokenOrNull = readMacOsCredentialOrNull(accountName);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Secure Google Calendar credential storage is supported on Windows and macOS.");
        }

        return Task.FromResult(refreshTokenOrNull);
    }

    public Task SaveRefreshTokenAsync(
        GoogleOAuthClientId clientId,
        GoogleRefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        if (refreshToken == null)
        {
            throw new ArgumentNullException(nameof(refreshToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string accountName = createAccountName(clientId);
        if (OperatingSystem.IsWindows())
        {
            saveWindowsCredential(accountName, refreshToken);
        }
        else if (OperatingSystem.IsMacOS())
        {
            saveMacOsCredential(accountName, refreshToken);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Secure Google Calendar credential storage is supported on Windows and macOS.");
        }

        return Task.CompletedTask;
    }

    public Task DeleteRefreshTokenAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string accountName = createAccountName(clientId);
        if (OperatingSystem.IsWindows())
        {
            deleteWindowsCredential(accountName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            deleteMacOsCredential(accountName);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Secure Google Calendar credential storage is supported on Windows and macOS.");
        }

        return Task.CompletedTask;
    }

    private static string createAccountName(GoogleOAuthClientId clientId)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(clientId.Value));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

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
        IntPtr tokenPointer = Marshal.AllocCoTaskMem(tokenBytes.Length);
        try
        {
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

    private static GoogleRefreshToken? readMacOsCredentialOrNull(string accountName)
    {
        byte[] serviceNameBytes = Encoding.UTF8.GetBytes(CREDENTIAL_SERVICE_NAME);
        byte[] accountNameBytes = Encoding.UTF8.GetBytes(accountName);
        uint passwordLength;
        IntPtr passwordData;
        IntPtr itemReference;
        int status = secKeychainFindGenericPassword(
            IntPtr.Zero,
            checked((uint)serviceNameBytes.Length),
            serviceNameBytes,
            checked((uint)accountNameBytes.Length),
            accountNameBytes,
            out passwordLength,
            out passwordData,
            out itemReference);
        if (status == MACOS_ITEM_NOT_FOUND_STATUS)
        {
            return null;
        }

        ensureMacOsSuccess(status);
        try
        {
            byte[] tokenBytes = new byte[passwordLength];
            Marshal.Copy(passwordData, tokenBytes, 0, tokenBytes.Length);
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
            secKeychainItemFreeContent(IntPtr.Zero, passwordData);
            if (itemReference != IntPtr.Zero)
            {
                cfRelease(itemReference);
            }
        }
    }

    private static void saveMacOsCredential(
        string accountName,
        GoogleRefreshToken refreshToken)
    {
        byte[] serviceNameBytes = Encoding.UTF8.GetBytes(CREDENTIAL_SERVICE_NAME);
        byte[] accountNameBytes = Encoding.UTF8.GetBytes(accountName);
        byte[] tokenBytes = Encoding.UTF8.GetBytes(refreshToken.Value);
        try
        {
            IntPtr existingPasswordData;
            IntPtr itemReference;
            int findStatus = secKeychainFindGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceNameBytes.Length),
                serviceNameBytes,
                checked((uint)accountNameBytes.Length),
                accountNameBytes,
                out _,
                out existingPasswordData,
                out itemReference);
            if (findStatus == 0)
            {
                try
                {
                    secKeychainItemFreeContent(IntPtr.Zero, existingPasswordData);
                    ensureMacOsSuccess(
                        secKeychainItemModifyAttributesAndData(
                            itemReference,
                            IntPtr.Zero,
                            checked((uint)tokenBytes.Length),
                            tokenBytes));
                }
                finally
                {
                    cfRelease(itemReference);
                }

                return;
            }

            if (findStatus != MACOS_ITEM_NOT_FOUND_STATUS)
            {
                ensureMacOsSuccess(findStatus);
            }

            IntPtr createdItemReference;
            int createStatus = secKeychainAddGenericPassword(
                IntPtr.Zero,
                checked((uint)serviceNameBytes.Length),
                serviceNameBytes,
                checked((uint)accountNameBytes.Length),
                accountNameBytes,
                checked((uint)tokenBytes.Length),
                tokenBytes,
                out createdItemReference);
            ensureMacOsSuccess(createStatus);
            if (createdItemReference != IntPtr.Zero)
            {
                cfRelease(createdItemReference);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    private static void deleteMacOsCredential(string accountName)
    {
        byte[] serviceNameBytes = Encoding.UTF8.GetBytes(CREDENTIAL_SERVICE_NAME);
        byte[] accountNameBytes = Encoding.UTF8.GetBytes(accountName);
        IntPtr passwordData;
        IntPtr itemReference;
        int status = secKeychainFindGenericPassword(
            IntPtr.Zero,
            checked((uint)serviceNameBytes.Length),
            serviceNameBytes,
            checked((uint)accountNameBytes.Length),
            accountNameBytes,
            out _,
            out passwordData,
            out itemReference);
        if (status == MACOS_ITEM_NOT_FOUND_STATUS)
        {
            return;
        }

        ensureMacOsSuccess(status);
        try
        {
            secKeychainItemFreeContent(IntPtr.Zero, passwordData);
            ensureMacOsSuccess(secKeychainItemDelete(itemReference));
        }
        finally
        {
            cfRelease(itemReference);
        }
    }

    private static void ensureMacOsSuccess(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(
                "The macOS Keychain operation failed with status " + status + ".");
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

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainFindGenericPassword")]
    private static extern int secKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemReference);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainAddGenericPassword")]
    private static extern int secKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemReference);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainItemModifyAttributesAndData")]
    private static extern int secKeychainItemModifyAttributesAndData(
        IntPtr itemReference,
        IntPtr attributes,
        uint dataLength,
        byte[] data);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainItemDelete")]
    private static extern int secKeychainItemDelete(IntPtr itemReference);

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainItemFreeContent")]
    private static extern int secKeychainItemFreeContent(
        IntPtr attributes,
        IntPtr data);

    [DllImport(
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        EntryPoint = "CFRelease")]
    private static extern void cfRelease(IntPtr value);
}
