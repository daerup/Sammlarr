using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.Extensions.Options;

namespace UI.Data.Stores;

public sealed class PlexWatchListStore(
	IHttpClientFactory httpClientFactory,
	ICredentialStore credentialStore,
	IOptionsMonitor<PlexAuthenticationOptions> options
) : IWatchlistStore
{
	private const string WatchlistEndpoint = "https://discover.provider.plex.tv/library/sections/watchlist/all";
	private const string LegacyWatchlistEndpoint = "https://metadata.provider.plex.tv/library/sections/watchlist/all";
	private static readonly string[] WatchlistEndpoints =
	{
		WatchlistEndpoint,
		"https://discover.provider.plex.tv/library/sections/watchlist/all?includeCollections=1&includeExternalMedia=1",
		"https://metadata.provider.plex.tv/library/sections/watchlist/all?includeCollections=1&includeExternalMedia=1",
		LegacyWatchlistEndpoint,
	};

	public async Task<IReadOnlyList<PlexWatchlistItem>> GetWatchlistAsync(CancellationToken cancellationToken = default)
	{
		var accessToken = await GetAccessTokenAsync(cancellationToken);

		HttpResponseMessage? response = null;
		var attemptedUris = new List<string>(WatchlistEndpoints.Length);
		foreach (var endpoint in WatchlistEndpoints)
		{
			var endpointWithToken = AppendTokenQuery(endpoint, accessToken);
			attemptedUris.Add(endpointWithToken);

			using var request = CreatePlexRequest(HttpMethod.Get, endpointWithToken, accessToken);
			response = await CreateClient().SendAsync(request, cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				break;
			}

			if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
			{
				break;
			}

			response.Dispose();
			response = null;
		}

		if (response is null)
		{
			throw new HttpRequestException($"Plex watchlist endpoint not found. Attempted: {string.Join(", ", attemptedUris)}");
		}

		if (!response.IsSuccessStatusCode)
		{
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
			throw new HttpRequestException(
				$"Plex watchlist request failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {Trim(responseBody, 400)}"
			);
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var items = new List<PlexWatchlistItem>();
		if (
			document.RootElement.TryGetProperty("MediaContainer", out var container)
			&& container.TryGetProperty("Metadata", out var metadataArray)
			&& metadataArray.ValueKind == JsonValueKind.Array
		)
		{
			items = ParseItems(metadataArray);
		}
		else if (document.RootElement.TryGetProperty("items", out var itemsArray) && itemsArray.ValueKind == JsonValueKind.Array)
		{
			items = ParseItems(itemsArray);
		}

		return items;
	}

	public async Task<bool> AddToWatchlistAsync(string ratingKey, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(ratingKey))
		{
			throw new ArgumentException("Rating key is required to add an item to the Plex watchlist.", nameof(ratingKey));
		}

		var accessToken = await GetAccessTokenAsync(cancellationToken);

		var endpoint = $"https://vod.provider.plex.tv/actions/addToWatchlist?ratingKey={Uri.EscapeDataString(ratingKey)}";
		using var request = CreatePlexRequest(HttpMethod.Put, AppendTokenQuery(endpoint, accessToken), accessToken);
		request.Content = new StringContent(string.Empty);

		var response = await CreateClient().SendAsync(request, cancellationToken);
		return response.IsSuccessStatusCode;
	}

	private HttpClient CreateClient() => httpClientFactory.CreateClient("Plex");

	private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
	{
		var credential = await credentialStore.GetAsync(CredentialProvider.Plex, cancellationToken);

		if (credential is null || string.IsNullOrWhiteSpace(credential.AccessToken))
		{
			throw new InvalidOperationException("No Plex credentials are stored. Use the Plex OAuth page to sign in first.");
		}

		return credential.AccessToken;
	}

	private HttpRequestMessage CreatePlexRequest(HttpMethod method, string uri, string accessToken)
	{
		var request = new HttpRequestMessage(method, uri);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		request.Headers.Add("X-Plex-Token", accessToken);

		var opts = options.CurrentValue;
		if (!string.IsNullOrWhiteSpace(opts.ClientId))
		{
			request.Headers.Add("X-Plex-Client-Identifier", opts.ClientId);
		}

		request.Headers.Add("X-Plex-Product", opts.ProductName);
		request.Headers.Add("X-Plex-Version", "1.0");
		request.Headers.Add("X-Plex-Device", "Web");
		request.Headers.Add("X-Plex-Device-Name", opts.ProductName);
		request.Headers.Add("X-Plex-Platform", "Web");
		return request;
	}

	private static List<PlexWatchlistItem> ParseItems(JsonElement array)
	{
		var items = new List<PlexWatchlistItem>(array.GetArrayLength());
		foreach (var element in array.EnumerateArray())
		{
			items.Add(PlexWatchlistItem.FromJson(element));
		}

		return items;
	}

	private static string AppendTokenQuery(string uri, string accessToken)
	{
		var separator = uri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
		return $"{uri}{separator}X-Plex-Token={Uri.EscapeDataString(accessToken)}";
	}

	private static string Trim(string value, int maxLength)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
		{
			return value;
		}

		return value[..maxLength] + "...";
	}
}
