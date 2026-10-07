using System.Globalization;
using API.AniList.Types;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.AniList;

internal sealed class AniListEnricher(AniListTitles titles) : IDetailsEnricher
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
		| MediaFields.Rated(RemoteService.AniList.SuppliedRatingSources());

	public MediaFields Defers { get; } = MediaField.Title;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.AniList];

	public async Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		if (!int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
		{
			return Error.From($"'{id.Value}' is no AniList id.");
		}

		var answer = await titles.FindAsync(number, cancellationToken);
		return answer.Map(media => Found(id, media));
	}

	private static FoundMedia Found(IExternalMediaId id, Optional<AniListTypes.Media> media)
	{
		return media.Match(
			found => new FoundMedia([.. AniListMedia.Ids(found, Type(id, found))], AniListMedia.Details(found, Type(id, found))),
			() => new FoundMedia([id], MediaDetails.Empty(id.Type))
		);
	}

	private static MediaType Type(IExternalMediaId id, AniListTypes.Media media) => id.Type is MediaType.Unknown ? AniListMedia.TypeOf(media) : id.Type;
}
