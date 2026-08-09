using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class NativeEventKitCalendarCommand : IEventKitCalendarCommand
{
    private const uint SUPPORTED_ABI_VERSION = 1;
    private const uint SUPPORTED_SCHEMA_VERSION = 1;
    private const string LIBRARY_FILE_NAME = "libTimetableGenerator.EventKitBridge.dylib";

    private static readonly CancellationRequestedDelegate CANCELLATION_REQUESTED = isCancellationRequested;

    private static readonly Lazy<NativeApi?> NATIVE_API = new Lazy<NativeApi?>(loadNativeApiOrNull, LazyThreadSafetyMode.ExecutionAndPublication);

    public bool IsAvailable
    {
        get
        {
            return NATIVE_API.Value != null;
        }
    }

    public Task<string> ExecuteAsync(string requestJson, CancellationToken cancellationToken)
    {
        if (requestJson == null)
        {
            throw new ArgumentNullException(nameof(requestJson));
        }

        cancellationToken.ThrowIfCancellationRequested();
        NativeApi? nativeApiOrNull = NATIVE_API.Value;
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
            VersionDelegate getAbiVersion = Marshal.GetDelegateForFunctionPointer<VersionDelegate>(NativeLibrary.GetExport(libraryHandle, "tg_eventkit_abi_version"));
            VersionDelegate getSchemaVersion = Marshal.GetDelegateForFunctionPointer<VersionDelegate>(NativeLibrary.GetExport(libraryHandle, "tg_eventkit_schema_version"));
            if (getAbiVersion() != SUPPORTED_ABI_VERSION || getSchemaVersion() != SUPPORTED_SCHEMA_VERSION)
            {
                NativeLibrary.Free(libraryHandle);
                return null;
            }

            ExecuteCancellableDelegate executeCommand = Marshal.GetDelegateForFunctionPointer<ExecuteCancellableDelegate>(NativeLibrary.GetExport(libraryHandle, "tg_eventkit_execute_cancellable"));
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
        GCHandle cancellationContext = GCHandle.Alloc(cancellationToken);
        IntPtr responsePointer = IntPtr.Zero;
        try
        {
            responsePointer = nativeApi.Execute(
                pinnedRequest.AddrOfPinnedObject(),
                checked((nuint)requestBytes.Length),
                CANCELLATION_REQUESTED,
                GCHandle.ToIntPtr(cancellationContext));
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

            GC.KeepAlive(CANCELLATION_REQUESTED);
            pinnedRequest.Free();
            cancellationContext.Free();
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint VersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CancellationRequestedDelegate(IntPtr cancellationContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ExecuteCancellableDelegate(
        IntPtr requestBytes,
        nuint requestLength,
        CancellationRequestedDelegate cancellationRequested,
        IntPtr cancellationContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeResponseDelegate(IntPtr response);

    private sealed class NativeApi
    {
        public IntPtr LibraryHandle { get; }

        public ExecuteCancellableDelegate Execute { get; }

        public FreeResponseDelegate FreeResponse { get; }

        public NativeApi(IntPtr libraryHandle, ExecuteCancellableDelegate execute, FreeResponseDelegate freeResponse)
        {
            LibraryHandle = libraryHandle;
            Execute = execute;
            FreeResponse = freeResponse;
        }
    }

    private static int isCancellationRequested(IntPtr cancellationContext)
    {
        try
        {
            object? targetOrNull = GCHandle.FromIntPtr(cancellationContext).Target;
            if (targetOrNull is CancellationToken cancellationToken && cancellationToken.IsCancellationRequested)
            {
                return 1;
            }
        }
        catch (InvalidOperationException)
        {
        }

        return 0;
    }
}
