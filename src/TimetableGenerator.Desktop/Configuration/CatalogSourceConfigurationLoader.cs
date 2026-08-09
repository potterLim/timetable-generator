using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Storage;

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

    internal CatalogSourceConfigurationLoader(CatalogSourceConfigurationPath path, Func<string?> environmentValueProvider)
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
            throw new CatalogSourceConfigurationException("No catalog source is configured for this installation.");
        }

        try
        {
            byte[] content = await BoundedLocalFileReader.readAllBytesAsync(mPath.Value, MAXIMUM_CONFIGURATION_FILE_BYTES, cancellationToken).ConfigureAwait(false);
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
        catch (BoundedLocalFileReadLimitException exception)
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration exceeds the product size limit.", exception);
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration could not be read.", exception);
        }
    }

    private static bool isFileSystemException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException;
    }
}
