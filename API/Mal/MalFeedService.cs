using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using API.Mal.Types;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;

namespace API.Mal;

internal sealed partial class MalFeedService(MalClient client, IMediaService media, IApiKeyService apiKeys) : IFeedProvider
{
	private const string Fields = "list_status,media_type,start_date,alternative_titles";

	private static readonly Error NotAList = Error.From("Paste a MyAnimeList username or the list's myanimelist.net/animelist link.");

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		var match = ListPattern().Match(input.Trim());
		return Task.FromResult(Optional.When(match.Success, () => match.Groups["user"].Value).ToResult(NotAList));
	}

	public IAsyncEnumerable<MediaMatchResult> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default)
	{
		return media.IdentifyWithDetailsAsync(RequestsAsync(identifier, cancellationToken), cancellationToken);
	}

	private async IAsyncEnumerable<IdentifyRequest> RequestsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var clientId = await apiKeys
			.GetAsync(ApiKeyProvider.Mal, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No MyAnimeList client ID is configured. Add one in Settings."));
		var page = string.Format(CultureInfo.InvariantCulture, "users/{0}/animelist?fields={1}&limit=1000", Uri.EscapeDataString(identifier), Fields);
		while (page.Length > 0)
		{
			var answer = await PageAsync(identifier, page, clientId, cancellationToken);
			foreach (var anime in (answer.Data ?? []).Select(item => item.Node).OfType<MalTypes.Anime>())
			{
				var type = MalAnime.TypeOf(anime);
				yield return new IdentifyRequest([MalAnime.Id(anime, type)], MalAnime.Hints(anime, type));
			}

			page = ProviderValues.Text(answer.Paging?.Next) ?? string.Empty;
		}
	}

	private async Task<MalTypes.ListPage> PageAsync(string identifier, string page, IApiKey clientId, CancellationToken cancellationToken)
	{
		using var response = await client.GetAsync(page, clientId, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			throw new FeedIdentifierNotFoundException(identifier);
		}

		if (response.StatusCode is HttpStatusCode.Forbidden)
		{
			throw new InvalidOperationException($"MyAnimeList keeps the list of {identifier} private, so Sammlarr cannot read it.");
		}

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<MalTypes.ListPage>(cancellationToken)
			?? throw new InvalidDataException("MyAnimeList answered an empty list page.");
	}

	[GeneratedRegex(@"^(?:(?:https?://)?(?:www\.)?myanimelist\.net/(?:animelist|profile)/)?(?<user>[A-Za-z0-9_-]{2,16})/?(?:[?#].*)?$")]
	private static partial Regex ListPattern();
}
