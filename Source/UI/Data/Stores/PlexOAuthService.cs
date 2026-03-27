using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace UI.Data.Stores;

public sealed class PlexPinResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string AuthUrl { get; set; } = string.Empty;
    public long ExpiresIn { get; set; }
    public bool Authenticated { get; set; }
    public string? AuthToken { get; set; }
}

public sealed class PlexOAuthService(IHttpClientFactory httpClientFactory, IOptionsMonitor<PlexAuthenticationOptions> options) : IPlexOAuthService
{
    private const string PlexAuthAppUrl = "https://app.plex.tv/auth#";

    public Uri GetAuthorizationUri(string state) =>
        new(QueryHelpers.AddQueryString($"{options.CurrentValue.ProductName}/auth", new Dictionary<string, string?>
        {
            ["clientID"] = options.CurrentValue.ClientId,
            ["code"] = state
        }));
    private const string PlexAuthAppUrl = "https://app.plex.tv/auth";

    public async Task<PlexPinResponse> RequestPinAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(opts.ClientId))
        {
            throw new InvalidOperationException("Plex ClientId is not configured. Add a stable UUID to Plex:Authentication:ClientId in appsettings.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, PlexPinEndpoint);
        AddPlexHeaders(request, opts);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["strong"] = "true",
            ["X-Plex-Client-Identifier"] = opts.ClientId,
            ["X-Plex-Product"] = opts.ProductName
        });

        var response = await CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        // Build the URL the user visits to approve the request
        var authUrl = QueryHelpers.AddQueryString(PlexAuthAppUrl, new Dictionary<string, string?>
        {
            ["clientID"] = opts.ClientId,
        {
            queryParams["forwardUrl"] = forwardUrl;
        }

        var fragmentQuery = string.Join("&",
            queryParams
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return string.IsNullOrEmpty(fragmentQuery)
            ? PlexAuthAppUrl
            : $"{PlexAuthAppUrl}#?{fragmentQuery}";
    }

    public async Task<PlexPinResponse> CheckPinStatusAsync(int pinId, CancellationToken cancellationToken = default)
    {
        var opts = options.CurrentValue;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PlexPinEndpoint}/{pinId}");
        AddPlexHeaders(request, opts);

        var response = await CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var authToken = root.TryGetProperty("authToken", out var tokenProp) && tokenProp.ValueKind != JsonValueKind.Null
            ? tokenProp.GetString()
            : null;

        return new PlexPinResponse
        {
            Id = pinId,
            Authenticated = !string.IsNullOrWhiteSpace(authToken),
            AuthToken = authToken
        };
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, PlexUserEndpoint);
        AddPlexHeaders(request, options.CurrentValue);
        request.Headers.Add("X-Plex-Token", token);

        try
        {
    public async Task<PlexTokenResponse> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default)
        // Not used in the PIN flow — kept for interface compatibility.
        throw new NotSupportedException("Use the PIN-based flow via RequestPinAsync/CheckPinStatusAsync instead.");
        request.Headers.Add("X-Plex-Device", "Web");
        request.Headers.Add("X-Plex-Device-Name", opts.ProductName);
        request.Headers.Add("X-Plex-Platform", "Web");
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient("Plex");
}
