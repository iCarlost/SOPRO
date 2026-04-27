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
    /// Nota: para repositorios privados, GitHub requiere autenticación; en ese caso la consulta puede responder 404/401.
    /// </summary>
    public sealed class GitHubUpdateService
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/iCarlost/SOPRO-Releases/releases/latest";
        private const string InstallerExtension = ".exe";

        public async Task<UpdateCheckResult> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
        {
            if (currentVersion == null)
                throw new ArgumentNullException(nameof(currentVersion));

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SOPRO-Updater/1.0");
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await httpClient.GetAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Unavailable(
                    $"GitHub respondió {(int)response.StatusCode} {response.ReasonPhrase}. " +
                    "Si el repositorio es privado, la verificación automática requiere un origen público de actualizaciones o autenticación.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;

            if (!TryParseGitHubVersion(tagName, out var latestVersion))
                return UpdateCheckResult.Unavailable("El release más reciente no tiene una etiqueta de versión válida.");

            var releaseUrl = root.TryGetProperty("html_url", out var htmlElement)
                ? htmlElement.GetString() ?? string.Empty
                : string.Empty;

            var installerUrl = GetInstallerUrl(root) ?? releaseUrl;
            var hasUpdate = latestVersion.CompareTo(currentVersion) > 0;

            return UpdateCheckResult.Available(
                hasUpdate,
                tagName ?? $"v{latestVersion}",
                latestVersion,
                installerUrl,
                releaseUrl);
        }

        private static string? GetInstallerUrl(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            var candidates = new List<(string Name, string Url)>();
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;

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
