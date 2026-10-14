using System.Globalization;
using System.Net;
using API.Http;
using API.Mal.Types;
using Contracts.Auth;
using Contracts.Media;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;

namespace API.Mal;

internal sealed class MalEnricher(MalClient client, IApiKeyService apiKeys) : IDetailsEnricher
{
	public MediaFields Fills { get; } =
		MediaField.Title
		| MediaField.Summary
		| MediaField.Poster
		| MediaField.ReleaseDate
		| MediaField.Genres
		| MediaField.Runtime
		| MediaField.Episodes
		| MediaField.RuntimePerEpisode
		| MediaFields.Rated(RemoteService.Mal.SuppliedRatingSources());

	public MediaFields Defers { get; } = MediaField.Title;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Mal];

	public Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, ValueSet<IIdentityKey> keys, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(id, cancellationToken).GuardedAsync(RemoteService.Mal);
	}

	private async Task<Result<FoundMedia>> LookUpResultAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		if (!int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
		{
			return Error.From($"'{id.Value}' is no MyAnimeList id.");
		}

		var clientId = await apiKeys.GetAsync(ApiKeyProvider.Mal, cancellationToken);
		return await clientId.Map(found => AskAsync(id, number, found, cancellationToken));
	}

	private async Task<Result<FoundMedia>> AskAsync(IExternalMediaId id, int number, IApiKey clientId, CancellationToken cancellationToken)
	{
		using var response = await client.GetAsync(FormattableString.Invariant($"anime/{number}?fields={MalAnime.Fields}"), clientId, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			return Success.Of(new FoundMedia([id], MediaDetails.Empty(id.Type)));
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(
				RemoteService.Mal,
				FormattableString.CurrentCulture($"MyAnimeList answered {(int)response.StatusCode} for anime {number}.")
			);
		}

		var anime =
			await response.Content.ReadFromJsonAsync<MalTypes.Anime>(cancellationToken) ?? throw new InvalidDataException("MyAnimeList answered an empty anime.");
		var type = id.Type is MediaType.Unknown ? MalAnime.TypeOf(anime) : id.Type;
		return Success.Of(new FoundMedia([MalAnime.Id(anime, type)], MalAnime.Details(anime, type)));
	}
}
