using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed partial class OperatingSystemGoogleCalendarCredentialStore
{
    private const int MACOS_ITEM_NOT_FOUND_STATUS = -25300;

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

            IntPtr createdItemReference = IntPtr.Zero;
            try
            {
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
            }
            finally
            {
                if (createdItemReference != IntPtr.Zero)
                {
                    cfRelease(createdItemReference);
                }
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
