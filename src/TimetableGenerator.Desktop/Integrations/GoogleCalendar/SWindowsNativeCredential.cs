using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[SuppressMessage(
    "Style",
    "IDE0044:Add readonly modifier",
    Justification = "Windows credential APIs populate these sequential-layout fields during native marshalling.")]
internal struct SWindowsNativeCredential
{
    private uint mFlags;
    private uint mType;

    [MarshalAs(UnmanagedType.LPWStr)]
    private string? mTargetNameOrNull;

    [MarshalAs(UnmanagedType.LPWStr)]
    private string? mCommentOrNull;

    private long mLastWritten;
    private uint mCredentialBlobSize;
    private IntPtr mCredentialBlob;
    private uint mPersist;
    private uint mAttributeCount;
    private IntPtr mAttributes;

    [MarshalAs(UnmanagedType.LPWStr)]
    private string? mTargetAliasOrNull;

    [MarshalAs(UnmanagedType.LPWStr)]
    private string? mUserNameOrNull;

    public uint CredentialBlobSize
    {
        get
        {
            return mCredentialBlobSize;
        }
    }

    public IntPtr CredentialBlob
    {
        get
        {
            return mCredentialBlob;
        }
    }

    public SWindowsNativeCredential(
        uint type,
        string targetName,
        uint credentialBlobSize,
        IntPtr credentialBlob,
        uint persist,
        string userName)
    {
        mFlags = 0U;
        mType = type;
        mTargetNameOrNull = targetName;
        mCommentOrNull = null;
        mLastWritten = 0L;
        mCredentialBlobSize = credentialBlobSize;
        mCredentialBlob = credentialBlob;
        mPersist = persist;
        mAttributeCount = 0U;
        mAttributes = IntPtr.Zero;
        mTargetAliasOrNull = null;
        mUserNameOrNull = userName;
    }
}
