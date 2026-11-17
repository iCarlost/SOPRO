using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Consulta el último release publicado de SOPRO en GitHub.
    /// Nota: si el origen de actualizaciones no es público, GitHub puede requerir autenticación y responder 404/401.
    /// </summary>
    public sealed class GitHubUpdateService
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/iCarlost/SOPRO-Releases/releases/latest";
        private const string InstallerExtension = ".exe";
        private const string UserAgent = "SOPRO-Updater/1.0";
        private const string AcceptHeader = "application/vnd.github+json";

        private static readonly Uri LatestReleaseApiUri = new(LatestReleaseApiUrl);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient? _httpClient;

        public GitHubUpdateService()
        {
        }

        internal GitHubUpdateService(HttpMessageHandler handler)
            : this(new HttpClient(handler, disposeHandler: false))
        {
        }

        internal GitHubUpdateService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(currentVersion);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(RequestTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUri);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd(AcceptHeader);

            var client = _httpClient ?? new HttpClient();
            var ownsClient = _httpClient is null;

            try
            {
                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return UpdateCheckResult.Unavailable(
                        $"GitHub respondió {(int)response.StatusCode}. " +
                        "Si el origen de actualizaciones no es público, la verificación automática requiere autenticación.");
                }

                await using var stream = await response.Content
                    .ReadAsStreamAsync(timeoutSource.Token)
                    .ConfigureAwait(false);

                try
                {
                    using var document = await JsonDocument
                        .ParseAsync(stream, cancellationToken: timeoutSource.Token)
                        .ConfigureAwait(false);

                    return ParseRelease(document.RootElement, currentVersion);
                }
                catch (JsonException)
                {
                    return UpdateCheckResult.Unavailable("La respuesta de GitHub no contiene un JSON válido.");
                }
            }
            finally
            {
                if (ownsClient)
                {
                    client.Dispose();
                }
            }
        }

        private static UpdateCheckResult ParseRelease(JsonElement root, Version currentVersion)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return UpdateCheckResult.Unavailable("La respuesta de GitHub no contiene un release válido.");
            }

            var tagName = TryGetString(root, "tag_name");
            if (!TryParseGitHubVersion(tagName, out var latestVersion))
                return UpdateCheckResult.Unavailable("El release más reciente no tiene una etiqueta de versión válida.");

            var releaseUrl = TryGetString(root, "html_url") ?? string.Empty;
            var installerUrl = GetInstallerUrl(root) ?? releaseUrl;
            var hasUpdate = latestVersion.CompareTo(currentVersion) > 0;

            return UpdateCheckResult.Available(
                hasUpdate,
                tagName ?? $"v{latestVersion}",
                latestVersion,
                installerUrl,
                releaseUrl);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private static string? GetInstallerUrl(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            var candidates = new List<(string Name, string Url)>();
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = TryGetString(asset, "name") ?? string.Empty;
                var url = TryGetString(asset, "browser_download_url") ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(name) &&
                    !string.IsNullOrWhiteSpace(url) &&
                    name.EndsWith(InstallerExtension, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((name, url));
                }
            }

            return candidates
                .OrderByDescending(c => c.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(c => c.Name)
                .Select(c => c.Url)
                .FirstOrDefault();
        }

        private static bool TryParseGitHubVersion(string? tagName, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(tagName))
                return false;

            var normalized = tagName.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[1..];

            if (Version.TryParse(normalized, out var parsedVersion))
            {
                version = parsedVersion;
                return true;
            }

            return false;
        }
    }

    public sealed class UpdateCheckResult
    {
        private UpdateCheckResult(
            bool checkSucceeded,
            bool hasUpdate,
            string latestTag,
            Version latestVersion,
            string installerUrl,
            string releaseUrl,
            string errorMessage)
        {
            CheckSucceeded = checkSucceeded;
            HasUpdate = hasUpdate;
            LatestTag = latestTag;
            LatestVersion = latestVersion;
            InstallerUrl = installerUrl;
            ReleaseUrl = releaseUrl;
            ErrorMessage = errorMessage;
        }

        public bool CheckSucceeded { get; }
        public bool HasUpdate { get; }
        public string LatestTag { get; }
        public Version LatestVersion { get; }
        public string InstallerUrl { get; }
        public string ReleaseUrl { get; }
        public string ErrorMessage { get; }

        public static UpdateCheckResult Available(bool hasUpdate, string latestTag, Version latestVersion, string installerUrl, string releaseUrl)
            => new(true, hasUpdate, latestTag, latestVersion, installerUrl, releaseUrl, string.Empty);

        public static UpdateCheckResult Unavailable(string errorMessage)
            => new(false, false, string.Empty, new Version(0, 0, 0), string.Empty, string.Empty, errorMessage);
    }
}
