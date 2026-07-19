using System;
using System.IO;
using System.Net.Http;
using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal static class GoogleCalendarIntegrationFactory
{
    private const string BINDING_FILE_NAME = "google-calendar-bindings-v1.json";
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
        FileGoogleCalendarBindingStore bindingStore =
            new FileGoogleCalendarBindingStore(
                new GoogleCalendarBindingFilePath(
                    Path.Combine(
                        dataRootPath.Value,
                        "Integrations",
                        BINDING_FILE_NAME)));
        ProductGoogleCalendarOAuthConfigurationProvider configurationProvider =
            new ProductGoogleCalendarOAuthConfigurationProvider();
        OperatingSystemGoogleCalendarCredentialStore credentialStore =
            new OperatingSystemGoogleCalendarCredentialStore();
        LoopbackGoogleOAuthAuthorizationCodeProvider codeProvider =
            new LoopbackGoogleOAuthAuthorizationCodeProvider(
                new DefaultExternalBrowserLauncher());
        GoogleCalendarOAuthClient oauthClient = new GoogleCalendarOAuthClient(
            httpClient,
            configurationProvider,
            credentialStore,
            codeProvider);
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(httpClient);
        FileGoogleCalendarExportLeaseProvider exportLeaseProvider =
            new FileGoogleCalendarExportLeaseProvider(
                new GoogleCalendarExportLockFilePath(
                    Path.Combine(
                        dataRootPath.Value,
                        "Integrations",
                        EXPORT_LOCK_FILE_NAME)));
        GoogleCalendarIntegrationResources resources =
            new GoogleCalendarIntegrationResources(httpClient, bindingStore);
        return new GoogleCalendarExportService(
            oauthClient,
            apiClient,
            bindingStore,
            exportLeaseProvider,
            resources);
    }

    private sealed class GoogleCalendarIntegrationResources : IDisposable
    {
        private readonly HttpClient mHttpClient;
        private readonly FileGoogleCalendarBindingStore mBindingStore;

        public GoogleCalendarIntegrationResources(
            HttpClient httpClient,
            FileGoogleCalendarBindingStore bindingStore)
        {
            mHttpClient = httpClient;
            mBindingStore = bindingStore;
        }

        public void Dispose()
        {
            mBindingStore.Dispose();
            mHttpClient.Dispose();
        }
    }
}
