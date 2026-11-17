using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services;

[TestClass]
public sealed class GitHubUpdateServiceTests
{
    private static readonly Version VersionActual = new(1, 19, 2);

    [TestMethod]
    public async Task CheckForUpdateAsync_HttpNoExitoso_DevuelveUnavailableSinRedReal()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "MOTIVO-SECRETO",
            Content = new StringContent("CUERPO-SECRETO", Encoding.UTF8, "text/plain")
        });
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsFalse(result.CheckSucceeded);
        Assert.IsFalse(result.HasUpdate);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.IsFalse(result.ErrorMessage.Contains("MOTIVO-SECRETO", StringComparison.Ordinal));
        Assert.IsFalse(result.ErrorMessage.Contains("CUERPO-SECRETO", StringComparison.Ordinal));
        Assert.IsNotNull(handler.LastRequestUri);
        Assert.AreEqual(HttpMethod.Get, handler.LastMethod);
        StringAssert.Contains(handler.LastUserAgent ?? string.Empty, "SOPRO-Updater/1.0");
        StringAssert.Contains(handler.LastAccept ?? string.Empty, "application/vnd.github+json");
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_JsonInvalido_DevuelveUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("{ esto no es json"));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsFalse(result.CheckSucceeded);
        Assert.IsFalse(result.HasUpdate);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_RaizNoObjeto_DevuelveUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("[]"));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsFalse(result.CheckSucceeded);
        Assert.IsFalse(result.HasUpdate);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_TagInvalido_DevuelveUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            { "tag_name": "release-sin-version", "html_url": "https://example.test/release" }
            """));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsFalse(result.CheckSucceeded);
        Assert.IsFalse(result.HasUpdate);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_VersionNueva_DevuelveActualizacionDisponible()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "tag_name": "v1.20.0",
              "html_url": "https://example.test/release/1.20.0",
              "assets": [
                { "name": "SOPRO-Setup-1.20.0.exe", "browser_download_url": "https://example.test/download/setup.exe" }
              ]
            }
            """));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsTrue(result.CheckSucceeded);
        Assert.IsTrue(result.HasUpdate);
        Assert.AreEqual("v1.20.0", result.LatestTag);
        Assert.AreEqual(new Version(1, 20, 0), result.LatestVersion);
        Assert.AreEqual("https://example.test/download/setup.exe", result.InstallerUrl);
        Assert.AreEqual("https://example.test/release/1.20.0", result.ReleaseUrl);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_MismaVersion_NoMarcaActualizacion()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "tag_name": "v1.19.2",
              "html_url": "https://example.test/release/1.19.2",
              "assets": [
                { "name": "SOPRO-Setup-1.19.2.exe", "browser_download_url": "https://example.test/download/setup.exe" }
              ]
            }
            """));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsTrue(result.CheckSucceeded);
        Assert.IsFalse(result.HasUpdate);
        Assert.AreEqual(new Version(1, 19, 2), result.LatestVersion);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_SinAssetExe_ConservaFallbackAlRelease()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "tag_name": "v1.20.0",
              "html_url": "https://example.test/release/1.20.0",
              "assets": [
                { "name": "SOPRO-1.20.0.zip", "browser_download_url": "https://example.test/download/source.zip" }
              ]
            }
            """));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsTrue(result.CheckSucceeded);
        Assert.IsTrue(result.HasUpdate);
        Assert.AreEqual(result.ReleaseUrl, result.InstallerUrl);
        Assert.AreEqual("https://example.test/release/1.20.0", result.InstallerUrl);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_JsonNulo_DevuelveUnavailable()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("null"));
        var service = new GitHubUpdateService(handler);

        var result = await service.CheckForUpdateAsync(VersionActual);

        Assert.IsFalse(result.CheckSucceeded);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastUserAgent { get; private set; }

        public string? LastAccept { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastUserAgent = request.Headers.UserAgent.ToString();
            LastAccept = request.Headers.Accept.ToString();
            return Task.FromResult(_responseFactory(request));
        }
    }
}
