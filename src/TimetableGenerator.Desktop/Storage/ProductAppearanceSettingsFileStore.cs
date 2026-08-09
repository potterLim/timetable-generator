using System;
using System.Diagnostics;
using System.IO;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Storage;

internal sealed class ProductAppearanceSettingsFileStore
    : IProductAppearanceSettingsStore
{
    private const long MAXIMUM_SETTINGS_FILE_BYTES = 16_384L;
    private const int WRITE_BUFFER_SIZE_BYTES = 4_096;

    private readonly ProductAppearanceSettingsFilePath mFilePath;

    private readonly ProductAppearanceSettingsJsonCodec mJsonCodec;

    public ProductAppearanceSettingsFileStore(ProductAppearanceSettingsFilePath filePath, ProductAppearanceSettingsJsonCodec jsonCodec)
    {
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (jsonCodec == null)
        {
            throw new ArgumentNullException(nameof(jsonCodec));
        }

        mFilePath = filePath;
        mJsonCodec = jsonCodec;
    }

    public ProductAppearanceSettings LoadOrDefault()
    {
        if (File.Exists(mFilePath.Value) == false)
        {
            return ProductAppearanceSettings.CreateDefault();
        }

        try
        {
            byte[] content = BoundedLocalFileReader.readAllBytes(mFilePath.Value, MAXIMUM_SETTINGS_FILE_BYTES);
            return mJsonCodec.Deserialize(content);
        }
        catch (Exception exception) when (canRecoverFromLoadFailure(exception))
        {
            Trace.TraceWarning("The appearance settings could not be loaded and system theme was restored: {0}", exception);
            return ProductAppearanceSettings.CreateDefault();
        }
    }

    public void Save(ProductAppearanceSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        string temporaryFilePath = createTemporaryFilePath();
        try
        {
            string? directoryPathOrNull = Path.GetDirectoryName(mFilePath.Value);
            if (directoryPathOrNull == null)
            {
                throw new InvalidOperationException("The appearance settings path has no parent directory.");
            }

            Directory.CreateDirectory(directoryPathOrNull);
            byte[] content = mJsonCodec.Serialize(settings);
            if (content.LongLength > MAXIMUM_SETTINGS_FILE_BYTES)
            {
                throw new ProductAppearanceSettingsException("The appearance settings exceed the product size limit.");
            }

            using (FileStream outputStream = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                WRITE_BUFFER_SIZE_BYTES,
                FileOptions.WriteThrough))
            {
                outputStream.Write(content, 0, content.Length);
                outputStream.Flush(true);
            }

            File.Move(temporaryFilePath, mFilePath.Value, true);
        }
        catch (Exception exception) when (canWrapSaveFailure(exception))
        {
            throw new ProductAppearanceSettingsException("The appearance settings could not be saved.", exception);
        }
        finally
        {
            tryDeleteTemporaryFile(temporaryFilePath);
        }
    }

    private string createTemporaryFilePath()
    {
        return mFilePath.Value + "." + Guid.NewGuid().ToString("N") + ".tmp";
    }

    private static bool canRecoverFromLoadFailure(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ProductAppearanceSettingsException;
    }

    private static bool canWrapSaveFailure(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is InvalidOperationException;
    }

    private static void tryDeleteTemporaryFile(string temporaryFilePath)
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Trace.TraceWarning("A temporary appearance settings file could not be removed: {0}", exception);
        }
    }
}
