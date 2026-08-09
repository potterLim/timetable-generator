using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using TimetableGenerator.Desktop.Configuration;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Configuration;

public sealed class CatalogSourceConfigurationTests
{
    [AvaloniaFact]
    public void JsonReaderCreatesStrictLocalFileConfiguration()
    {
        byte[] content = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"indexUri\":\"https://catalog.example/v1/index.json\"}");

        CatalogSourceConfiguration configuration = CatalogSourceConfigurationJsonReader.Read(content);

        Assert.Equal(ECatalogSourceOrigin.LocalFile, configuration.Origin);
        Assert.Equal("https://catalog.example/v1/index.json", configuration.Endpoint.Value.AbsoluteUri);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":2,\"indexUri\":\"https://catalog.example/index.json\"}")]
    [InlineData("{\"schemaVersion\":1,\"indexUri\":\"relative/index.json\"}")]
    [InlineData("{\"schemaVersion\":1,\"indexUri\":\"https://catalog.example/index.json\",\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"indexUri\":\"https://catalog.example/index.json\"}")]
    public void JsonReaderRejectsMalformedOrExpandedContracts(string json)
    {
        byte[] content = Encoding.UTF8.GetBytes(json);

        Assert.Throws<CatalogSourceConfigurationException>(
            delegate
            {
                CatalogSourceConfigurationJsonReader.Read(content);
            });
    }

    [AvaloniaFact]
    public async Task LoaderUsesEnvironmentConfigurationBeforeLocalFileAsync()
    {
        CatalogSourceConfigurationPath missingPath = createConfigurationPath("missing.json");
        CatalogSourceConfigurationLoader loader = new CatalogSourceConfigurationLoader(
            missingPath,
            delegate
            {
                return "https://environment.example/v1/index.json";
            });

        CatalogSourceConfiguration configuration = await loader.LoadAsync(CancellationToken.None);

        Assert.Equal(ECatalogSourceOrigin.Environment, configuration.Origin);
        Assert.Equal("https://environment.example/v1/index.json", configuration.Endpoint.Value.AbsoluteUri);
    }

    [AvaloniaFact]
    public async Task LoaderReadsLocalConfigurationWhenEnvironmentIsEmptyAsync()
    {
        CatalogSourceConfigurationPath path = createConfigurationPath("configured.json");
        string directoryPath = getDirectoryPath(path);
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(path.Value, "{\"schemaVersion\":1,\"indexUri\":\"https://file.example/v1/index.json\"}");
        CatalogSourceConfigurationLoader loader = new CatalogSourceConfigurationLoader(
            path,
            delegate
            {
                return null;
            });

        try
        {
            CatalogSourceConfiguration configuration = await loader.LoadAsync(CancellationToken.None);

            Assert.Equal(ECatalogSourceOrigin.LocalFile, configuration.Origin);
            Assert.Equal("https://file.example/v1/index.json", configuration.Endpoint.Value.AbsoluteUri);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task LoaderReportsMissingProductConfigurationAsync()
    {
        CatalogSourceConfigurationPath missingPath = createConfigurationPath("not-found.json");
        CatalogSourceConfigurationLoader loader = new CatalogSourceConfigurationLoader(
            missingPath,
            delegate
            {
                return null;
            });

        await Assert.ThrowsAsync<CatalogSourceConfigurationException>(
            async delegate
            {
                await loader.LoadAsync(CancellationToken.None);
            });
    }

    [AvaloniaFact]
    public async Task LoaderRejectsOversizedLocalConfigurationAsync()
    {
        CatalogSourceConfigurationPath path = createConfigurationPath("oversized.json");
        string directoryPath = getDirectoryPath(path);
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(path.Value, new byte[16_385], CancellationToken.None);
        CatalogSourceConfigurationLoader loader = new CatalogSourceConfigurationLoader(
            path,
            delegate
            {
                return null;
            });

        try
        {
            CatalogSourceConfigurationException exception = await Assert.ThrowsAsync<CatalogSourceConfigurationException>(
                async delegate
                {
                    await loader.LoadAsync(CancellationToken.None);
                });

            Assert.Contains("exceeds the product size limit", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    private static CatalogSourceConfigurationPath createConfigurationPath(string fileName)
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "TimetableGenerator.Desktop.Tests", Guid.NewGuid().ToString("N"));
        return new CatalogSourceConfigurationPath(Path.Combine(directoryPath, fileName));
    }

    private static string getDirectoryPath(CatalogSourceConfigurationPath path)
    {
        string? directoryPathOrNull = Path.GetDirectoryName(path.Value);
        if (directoryPathOrNull == null)
        {
            throw new InvalidOperationException("The test configuration path does not contain a directory.");
        }

        return directoryPathOrNull;
    }
}
