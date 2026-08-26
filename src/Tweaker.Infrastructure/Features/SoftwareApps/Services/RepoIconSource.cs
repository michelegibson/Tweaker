using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Tweaker.Core.Features.Common.Interfaces;
using Tweaker.Core.Features.SoftwareApps.Interfaces;

namespace Tweaker.Infrastructure.Features.SoftwareApps.Services;

public class RepoIconSource(HttpClient httpClient, ILogService logService) : IRepoIconSource
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("MODUNE_ICON_BASE_URL");
    private const string UserAgent = "Modune";
    private const long MaxIconBytes = 10L * 1024 * 1024;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    public async Task<byte[]?> GetIconBytesAsync(string repoPath, string? expectedSha256, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
                return null;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(FetchTimeout);
            using var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + repoPath.TrimStart('/'));
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            req.Headers.TryAddWithoutValidation("Accept", "image/*");
            using var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                logService.LogInformation($"RepoIcon: HTTP {(int)resp.StatusCode} for {repoPath}");
                return null;
            }
            if (resp.Content.Headers.ContentLength is long len && len > MaxIconBytes) return null;

            await using var src = await resp.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buf = new byte[81920]; int n;
            while ((n = await src.ReadAsync(buf, linked.Token).ConfigureAwait(false)) > 0)
            {
                if (ms.Length + n > MaxIconBytes) return null;
                await ms.WriteAsync(buf.AsMemory(0, n), linked.Token).ConfigureAwait(false);
            }
            var bytes = ms.ToArray();
            if (bytes.Length == 0) return null;

            if (expectedSha256 is not null)
            {
                var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    logService.LogWarning($"RepoIcon: sha256 mismatch for {repoPath}");
                    return null;
                }
            }
            if (!await DecodesAsImageAsync(bytes).ConfigureAwait(false))
            {
                logService.LogWarning($"RepoIcon: not a decodable image: {repoPath}");
                return null;
            }
            return bytes;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logService.LogWarning($"RepoIcon: {repoPath} failed: {ex.Message}"); return null; }
    }

    private static async Task<bool> DecodesAsImageAsync(byte[] bytes)
    {
        try
        {
            using var s = new InMemoryRandomAccessStream();
            await s.WriteAsync(bytes.AsBuffer());
            s.Seek(0);
            await BitmapDecoder.CreateAsync(s);
            return true;
        }
        catch { return false; }
    }
}
