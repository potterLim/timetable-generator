using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Configuration;

internal sealed class CatalogSourceConfigurationLoader
{
    private const long MAXIMUM_CONFIGURATION_FILE_BYTES = 16_384L;

    internal const string ENVIRONMENT_VARIABLE_NAME = "TIMETABLE_GENERATOR_CATALOG_INDEX_URI";

    private readonly CatalogSourceConfigurationPath mPath;

    private readonly Func<string?> mEnvironmentValueProvider;

    public CatalogSourceConfigurationLoader(CatalogSourceConfigurationPath path)
        : this(
            path,
            delegate
            {
                return Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE_NAME);
            })
    {
    }

    internal CatalogSourceConfigurationLoader(
        CatalogSourceConfigurationPath path,
        Func<string?> environmentValueProvider)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (environmentValueProvider == null)
        {
            throw new ArgumentNullException(nameof(environmentValueProvider));
        }

        mPath = path;
        mEnvironmentValueProvider = environmentValueProvider;
    }

    public async Task<CatalogSourceConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        string? environmentValueOrNull = mEnvironmentValueProvider();
        if (string.IsNullOrWhiteSpace(environmentValueOrNull) == false)
        {
            return CatalogSourceConfigurationJsonReader.createFromEnvironment(environmentValueOrNull);
        }

        if (File.Exists(mPath.Value) == false)
        {
            throw new CatalogSourceConfigurationException(
                "No catalog source is configured for this installation.");
        }

        try
        {
            FileInfo fileInfo = new FileInfo(mPath.Value);
            if (fileInfo.Length > MAXIMUM_CONFIGURATION_FILE_BYTES)
            {
                throw new CatalogSourceConfigurationException(
                    "The catalog source configuration exceeds the product size limit.");
            }

            byte[] content = await File.ReadAllBytesAsync(mPath.Value, cancellationToken).ConfigureAwait(false);
            return CatalogSourceConfigurationJsonReader.Read(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CatalogSourceConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new CatalogSourceConfigurationException(
                "The catalog source configuration could not be read.",
                exception);
        }
    }

    private static bool isFileSystemException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException;
    }
}
