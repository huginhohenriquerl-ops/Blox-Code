using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BloxCode.Core;

public sealed class ClientVersion
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("clientVersionUpload")] public string VersionGuid { get; set; } = "";
    [JsonPropertyName("bootstrapperVersion")] public string BootstrapperVersion { get; set; } = "";
}

public sealed class InvalidChannelException(string channel, HttpStatusCode? status)
    : Exception($"Channel '{channel}' is not available ({status?.ToString() ?? "no response"})");

/// <summary>
/// Talks to Roblox's deployment infrastructure. Endpoints and the mirror probe were
/// recovered from Bloxstrap.RobloxInterfaces.Deployment (see docs/REVERSE_ENGINEERING.md).
/// </summary>
public static class Deployment
{
    public const string DefaultChannel = "production";
    public const string BinaryType = "WindowsPlayer";

    private static readonly string[] Mirrors =
    {
        "https://setup.rbxcdn.com",
        "https://setup-aws.rbxcdn.com",
        "https://setup-ak.rbxcdn.com",
        "https://roblox-setup.cachefly.net",
        "https://s3.amazonaws.com/setup.roblox.com",
    };

    // /versionStudio on every mirror returns this frozen value, which makes it a cheap health probe.
    private const string VersionStudioProbe = "version-012732894899482c";

    public static string BaseUrl { get; private set; } = Mirrors[0];

    public static string GetLocation(string resource) => $"{BaseUrl}/channel/common{resource}";

    public static async Task InitializeConnectivity(HttpClient http, CancellationToken ct)
    {
        const string ident = "Deployment::InitializeConnectivity";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pending = Mirrors.Select(m => TestConnection(http, m, cts.Token)).ToList();
        var errors = new List<Exception>();

        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending);
            pending.Remove(finished);
            try
            {
                BaseUrl = await finished;
                Logger.Info(ident, $"Using mirror {BaseUrl}");
                cts.Cancel();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                errors.Add(ex);
            }
        }

        ct.ThrowIfCancellationRequested();
        throw new AggregateException("Could not reach any Roblox deployment mirror", errors);
    }

    private static async Task<string> TestConnection(HttpClient http, string mirror, CancellationToken ct)
    {
        string body = await http.GetStringAsync($"{mirror}/versionStudio", ct);
        if (body.Trim() != VersionStudioProbe)
            throw new InvalidDataException($"{mirror}/versionStudio returned an unexpected body");
        return mirror;
    }

    public static async Task<ClientVersion> GetInfo(HttpClient http, string channel, CancellationToken ct)
    {
        const string ident = "Deployment::GetInfo";
        // Production has no channel segment; ".../channel/production" answers 401 like an unknown channel.
        string path = channel == DefaultChannel
            ? $"/v2/client-version/{BinaryType}"
            : $"/v2/client-version/{BinaryType}/channel/{channel}";
        HttpStatusCode? lastStatus = null;

        foreach (var host in new[] { "https://clientsettingscdn.roblox.com", "https://clientsettings.roblox.com" })
        {
            try
            {
                using var response = await http.GetAsync(host + path, ct);
                lastStatus = response.StatusCode;
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                    throw new InvalidChannelException(channel, response.StatusCode);
                response.EnsureSuccessStatusCode();

                var info = await response.Content.ReadFromJsonAsync<ClientVersion>(ct)
                    ?? throw new InvalidDataException("Empty client-version response");
                Logger.Info(ident, $"{channel}: {info.Version} ({info.VersionGuid}) via {host}");
                return info;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                Logger.Warn(ident, $"{host} failed: {ex.Message}");
            }
        }

        throw new InvalidChannelException(channel, lastStatus);
    }
}
