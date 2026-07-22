using System;
using System.IO;
using System.Net.Http;
using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal static class GoogleCalendarIntegrationFactory
{
    private const string EXPORT_LOCK_FILE_NAME = "google-calendar-export.lock";

    public static IGoogleCalendarExporter Create(ProductDataRootPath dataRootPath)
    {
        if (dataRootPath == null)
        {
            throw new ArgumentNullException(nameof(dataRootPath));
        }

        HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30.0),
        };
        ProductGoogleCalendarOAuthConfigurationProvider configurationProvider = new ProductGoogleCalendarOAuthConfigurationProvider();
        LoopbackGoogleOAuthAuthorizationCodeProvider codeProvider = new LoopbackGoogleOAuthAuthorizationCodeProvider(new DefaultExternalBrowserLauncher());
        GoogleCalendarOAuthClient oauthClient = new GoogleCalendarOAuthClient(
            httpClient,
            configurationProvider,
            codeProvider);
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(httpClient);
        FileGoogleCalendarExportLeaseProvider exportLeaseProvider =
            new FileGoogleCalendarExportLeaseProvider(
                new GoogleCalendarExportLockFilePath(
                    Path.Combine(
                        dataRootPath.Value,
                        "Integrations",
                        EXPORT_LOCK_FILE_NAME)));
        GoogleCalendarIntegrationResources resources = new GoogleCalendarIntegrationResources(httpClient);
        return new GoogleCalendarExportService(oauthClient, apiClient, exportLeaseProvider, resources);
    }

    private sealed class GoogleCalendarIntegrationResources : IDisposable
    {
        private readonly HttpClient mHttpClient;

        public GoogleCalendarIntegrationResources(HttpClient httpClient)
        {
            mHttpClient = httpClient;
        }

        public void Dispose()
        {
            mHttpClient.Dispose();
        }
    }
}
