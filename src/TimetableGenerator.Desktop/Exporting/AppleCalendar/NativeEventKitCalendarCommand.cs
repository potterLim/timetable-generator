using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class NativeEventKitCalendarCommand : IEventKitCalendarCommand
{
    private const uint SUPPORTED_SCHEMA_VERSION = 1;
    private const string LIBRARY_FILE_NAME = "libTimetableGenerator.EventKitBridge.dylib";

    private readonly Lazy<NativeApi?> mNativeApi;

    public bool IsAvailable
    {
        get
        {
            return mNativeApi.Value != null;
        }
    }

    public NativeEventKitCalendarCommand()
    {
        mNativeApi = new Lazy<NativeApi?>(loadNativeApiOrNull, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<string> ExecuteAsync(string requestJson, CancellationToken cancellationToken)
    {
        if (requestJson == null)
        {
            throw new ArgumentNullException(nameof(requestJson));
        }

        cancellationToken.ThrowIfCancellationRequested();
        NativeApi? nativeApiOrNull = mNativeApi.Value;
        if (nativeApiOrNull == null)
        {
            throw new InvalidOperationException("The EventKit bridge is unavailable.");
        }

        return Task.Run(() => execute(nativeApiOrNull, requestJson, cancellationToken), cancellationToken);
    }

    private static NativeApi? loadNativeApiOrNull()
    {
        if (OperatingSystem.IsMacOSVersionAtLeast(14) == false)
        {
            return null;
        }

        string libraryPath = Path.Combine(AppContext.BaseDirectory, LIBRARY_FILE_NAME);
        IntPtr libraryHandle;
        if (NativeLibrary.TryLoad(libraryPath, out libraryHandle) == false)
        {
            return null;
        }

        try
        {
            SchemaVersionDelegate getSchemaVersion = Marshal.GetDelegateForFunctionPointer<SchemaVersionDelegate>(NativeLibrary.GetExport(libraryHandle, "tg_eventkit_schema_version"));
            if (getSchemaVersion() != SUPPORTED_SCHEMA_VERSION)
            {
                NativeLibrary.Free(libraryHandle);
                return null;
            }

            ExecuteDelegate executeCommand = Marshal.GetDelegateForFunctionPointer<ExecuteDelegate>(NativeLibrary.GetExport(libraryHandle, "tg_eventkit_execute"));
            FreeResponseDelegate freeResponse = Marshal.GetDelegateForFunctionPointer<FreeResponseDelegate>(NativeLibrary.GetExport(libraryHandle, "tg_eventkit_free"));
            return new NativeApi(libraryHandle, executeCommand, freeResponse);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is BadImageFormatException || exception is EntryPointNotFoundException)
        {
            NativeLibrary.Free(libraryHandle);
            return null;
        }
    }

    private static string execute(NativeApi nativeApi, string requestJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
        GCHandle pinnedRequest = GCHandle.Alloc(requestBytes, GCHandleType.Pinned);
        IntPtr responsePointer = IntPtr.Zero;
        try
        {
            responsePointer = nativeApi.Execute(pinnedRequest.AddrOfPinnedObject(), checked((nuint)requestBytes.Length));
            if (responsePointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("The EventKit bridge returned no response.");
            }

            string? responseOrNull = Marshal.PtrToStringUTF8(responsePointer);
            if (responseOrNull == null)
            {
                throw new InvalidOperationException("The EventKit bridge returned invalid UTF-8.");
            }

            return responseOrNull;
        }
        finally
        {
            if (responsePointer != IntPtr.Zero)
            {
                nativeApi.FreeResponse(responsePointer);
            }

            pinnedRequest.Free();
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint SchemaVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ExecuteDelegate(IntPtr requestBytes, nuint requestLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeResponseDelegate(IntPtr response);

    private sealed class NativeApi
    {
        public IntPtr LibraryHandle { get; }

        public ExecuteDelegate Execute { get; }

        public FreeResponseDelegate FreeResponse { get; }

        public NativeApi(IntPtr libraryHandle, ExecuteDelegate execute, FreeResponseDelegate freeResponse)
        {
            LibraryHandle = libraryHandle;
            Execute = execute;
            FreeResponse = freeResponse;
        }
    }
}
