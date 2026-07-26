using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using TimetableGenerator.Desktop.Presentation.Icons;

namespace TimetableGenerator.Desktop.Platforms.MacOS;

internal sealed class MacOSInstalledApplicationIconProvider
    : IInstalledApplicationIconProvider
{
    private const string OBJECTIVE_C_LIBRARY =
        "/usr/lib/libobjc.A.dylib";

    private const string CORE_GRAPHICS_FRAMEWORK =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private const uint BITMAP_BYTE_ORDER_32_LITTLE = 2U << 12;

    private const uint IMAGE_ALPHA_PREMULTIPLIED_FIRST = 2U;

    private const uint BGRA_PREMULTIPLIED_BITMAP_INFO =
        BITMAP_BYTE_ORDER_32_LITTLE | IMAGE_ALPHA_PREMULTIPLIED_FIRST;

    private const int HIGH_INTERPOLATION_QUALITY = 3;

    private const int BYTES_PER_PIXEL = 4;

    private const double DEFAULT_BITMAP_DPI = 96.0;

    public static MacOSInstalledApplicationIconProvider Instance { get; } =
        new MacOSInstalledApplicationIconProvider();

    private MacOSInstalledApplicationIconProvider()
    {
    }

    public Bitmap? TryLoad(string bundleIdentifier, PixelSize pixelSize)
    {
        InstalledApplicationIconRequest.Validate(
            bundleIdentifier,
            pixelSize);
        if (OperatingSystem.IsMacOS() == false)
        {
            return null;
        }

        try
        {
            return tryLoadNativeIcon(bundleIdentifier, pixelSize);
        }
        catch (Exception exception) when (isRecoverableInteropFailure(exception))
        {
            Trace.TraceWarning(
                "Installed macOS application icon loading failed: {0}",
                exception.GetType().Name);
            return null;
        }
    }

    private static Bitmap? tryLoadNativeIcon(
        string bundleIdentifier,
        PixelSize pixelSize)
    {
        IntPtr autoreleasePool = createAutoreleasePool();
        if (autoreleasePool == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            IntPtr image = findApplicationIcon(bundleIdentifier);
            if (image == IntPtr.Zero)
            {
                return null;
            }

            NativeRectangle proposedRectangle = new NativeRectangle(
                0.0,
                0.0,
                pixelSize.Width,
                pixelSize.Height);
            IntPtr imageSnapshot = sendMessage(
                image,
                registerSelector("CGImageForProposedRect:context:hints:"),
                ref proposedRectangle,
                IntPtr.Zero,
                IntPtr.Zero);
            if (imageSnapshot == IntPtr.Zero)
            {
                return null;
            }

            return createBitmap(imageSnapshot, pixelSize);
        }
        finally
        {
            sendVoidMessage(
                autoreleasePool,
                registerSelector("drain"));
        }
    }

    private static IntPtr createAutoreleasePool()
    {
        IntPtr autoreleasePoolClass = objcGetClass("NSAutoreleasePool");
        if (autoreleasePoolClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return sendMessage(
            autoreleasePoolClass,
            registerSelector("new"));
    }

    private static IntPtr findApplicationIcon(string bundleIdentifier)
    {
        IntPtr workspaceClass = objcGetClass("NSWorkspace");
        IntPtr stringClass = objcGetClass("NSString");
        if (workspaceClass == IntPtr.Zero || stringClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr workspace = sendMessage(
            workspaceClass,
            registerSelector("sharedWorkspace"));
        if (workspace == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr bundleIdentifierString = createNativeString(
            stringClass,
            bundleIdentifier);
        if (bundleIdentifierString == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr applicationUrl = sendMessage(
            workspace,
            registerSelector("URLForApplicationWithBundleIdentifier:"),
            bundleIdentifierString);
        if (applicationUrl == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr applicationPath = sendMessage(
            applicationUrl,
            registerSelector("path"));
        if (applicationPath == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return sendMessage(
            workspace,
            registerSelector("iconForFile:"),
            applicationPath);
    }

    private static IntPtr createNativeString(
        IntPtr stringClass,
        string value)
    {
        IntPtr utf8Bytes = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return sendMessage(
                stringClass,
                registerSelector("stringWithUTF8String:"),
                utf8Bytes);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8Bytes);
        }
    }

    private static Bitmap? createBitmap(
        IntPtr imageSnapshot,
        PixelSize pixelSize)
    {
        int rowByteCount = checked(pixelSize.Width * BYTES_PER_PIXEL);
        int pixelByteCount = checked(rowByteCount * pixelSize.Height);
        byte[] pixelBytes = new byte[pixelByteCount];
        IntPtr nativePixelBytes = Marshal.AllocHGlobal(pixelByteCount);
        try
        {
            Marshal.Copy(
                pixelBytes,
                0,
                nativePixelBytes,
                pixelByteCount);
            if (drawImage(
                    imageSnapshot,
                    pixelSize,
                    rowByteCount,
                    nativePixelBytes) == false)
            {
                return null;
            }

            Marshal.Copy(
                nativePixelBytes,
                pixelBytes,
                0,
                pixelByteCount);
            return createAvaloniaBitmap(
                pixelSize,
                rowByteCount,
                pixelBytes);
        }
        finally
        {
            Marshal.FreeHGlobal(nativePixelBytes);
        }
    }

    private static bool drawImage(
        IntPtr imageSnapshot,
        PixelSize pixelSize,
        int rowByteCount,
        IntPtr nativePixelBytes)
    {
        IntPtr colorSpace = cgColorSpaceCreateDeviceRgb();
        if (colorSpace == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr context = cgBitmapContextCreate(
                nativePixelBytes,
                (nuint)pixelSize.Width,
                (nuint)pixelSize.Height,
                8U,
                (nuint)rowByteCount,
                colorSpace,
                BGRA_PREMULTIPLIED_BITMAP_INFO);
            if (context == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                cgContextSetInterpolationQuality(
                    context,
                    HIGH_INTERPOLATION_QUALITY);
                cgContextDrawImage(
                    context,
                    new NativeRectangle(
                        0.0,
                        0.0,
                        pixelSize.Width,
                        pixelSize.Height),
                    imageSnapshot);
                return true;
            }
            finally
            {
                cgContextRelease(context);
            }
        }
        finally
        {
            cgColorSpaceRelease(colorSpace);
        }
    }

    private static Bitmap createAvaloniaBitmap(
        PixelSize pixelSize,
        int sourceRowByteCount,
        byte[] pixelBytes)
    {
        WriteableBitmap bitmap = new WriteableBitmap(
            pixelSize,
            new Vector(DEFAULT_BITMAP_DPI, DEFAULT_BITMAP_DPI),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        try
        {
            using (ILockedFramebuffer framebuffer = bitmap.Lock())
            {
                for (int rowIndex = 0;
                    rowIndex < pixelSize.Height;
                    ++rowIndex)
                {
                    Marshal.Copy(
                        pixelBytes,
                        rowIndex * sourceRowByteCount,
                        IntPtr.Add(
                            framebuffer.Address,
                            rowIndex * framebuffer.RowBytes),
                        sourceRowByteCount);
                }
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static bool isRecoverableInteropFailure(Exception exception)
    {
        return exception is BadImageFormatException
            || exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is ExternalException
            || exception is InvalidOperationException
            || exception is MarshalDirectiveException
            || exception is PlatformNotSupportedException
            || exception is TypeInitializationException;
    }

    private static IntPtr registerSelector(string selectorName)
    {
        return selRegisterName(selectorName);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRectangle
    {
        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public NativeRectangle(
            double x,
            double y,
            double width,
            double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    [DllImport(
        OBJECTIVE_C_LIBRARY,
        EntryPoint = "objc_getClass")]
    private static extern IntPtr objcGetClass(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string className);

    [DllImport(
        OBJECTIVE_C_LIBRARY,
        EntryPoint = "sel_registerName")]
    private static extern IntPtr selRegisterName(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string selectorName);

    [DllImport(
        OBJECTIVE_C_LIBRARY,
        EntryPoint = "objc_msgSend")]
    private static extern IntPtr sendMessage(
        IntPtr receiver,
        IntPtr selector);

    [DllImport(
        OBJECTIVE_C_LIBRARY,
        EntryPoint = "objc_msgSend")]
    private static extern IntPtr sendMessage(
        IntPtr receiver,
        IntPtr selector,
        IntPtr argument);

    [DllImport(
        OBJECTIVE_C_LIBRARY,
        EntryPoint = "objc_msgSend")]
    private static extern IntPtr sendMessage(
        IntPtr receiver,
        IntPtr selector,
        ref NativeRectangle rectangle,
        IntPtr context,
        IntPtr hints);

    [DllImport(
        OBJECTIVE_C_LIBRARY,
        EntryPoint = "objc_msgSend")]
    private static extern void sendVoidMessage(
        IntPtr receiver,
        IntPtr selector);

    [DllImport(
        CORE_GRAPHICS_FRAMEWORK,
        EntryPoint = "CGColorSpaceCreateDeviceRGB")]
    private static extern IntPtr cgColorSpaceCreateDeviceRgb();

    [DllImport(
        CORE_GRAPHICS_FRAMEWORK,
        EntryPoint = "CGColorSpaceRelease")]
    private static extern void cgColorSpaceRelease(IntPtr colorSpace);

    [DllImport(
        CORE_GRAPHICS_FRAMEWORK,
        EntryPoint = "CGBitmapContextCreate")]
    private static extern IntPtr cgBitmapContextCreate(
        IntPtr data,
        nuint width,
        nuint height,
        nuint bitsPerComponent,
        nuint bytesPerRow,
        IntPtr colorSpace,
        uint bitmapInfo);

    [DllImport(
        CORE_GRAPHICS_FRAMEWORK,
        EntryPoint = "CGContextSetInterpolationQuality")]
    private static extern void cgContextSetInterpolationQuality(
        IntPtr context,
        int quality);

    [DllImport(
        CORE_GRAPHICS_FRAMEWORK,
        EntryPoint = "CGContextDrawImage")]
    private static extern void cgContextDrawImage(
        IntPtr context,
        NativeRectangle rectangle,
        IntPtr image);

    [DllImport(
        CORE_GRAPHICS_FRAMEWORK,
        EntryPoint = "CGContextRelease")]
    private static extern void cgContextRelease(IntPtr context);
}
