using System.Text.Json;
using API.Mdblist.Types;
using Domain.Media;

namespace API.Mdblist;

internal static class MdblistFound
{
	internal static FoundMedia Of(MdblistTypes.Titles.Title title, MediaType type)
	{
		var ratings = title.Ratings ?? [];
		return new FoundMedia(
			[.. MdblistIds.Of(title.Ids, type), .. ratings.Select(rating => PageId(rating, type)).WhereSome()],
			MediaDetails.Empty(type) with
			{
				Ratings = MediaRatings.From(ratings.Select(Rate).WhereSome()),
			}
		);
	}

	private static Optional<IMediaRating> Rate(MdblistTypes.Titles.Rating rating)
	{
		return MdblistRatings
			.ByName.GetValueOrNone(rating.Source)
			.Map(source => Optional.Of(rating.Score).Map(score => source.Card.Rate(score / 10f, Optional.Of(rating.Votes))));
	}

	private static Optional<IExternalMediaId> PageId(MdblistTypes.Titles.Rating rating, MediaType type)
	{
		var path = Optional
			.When(rating.Url.ValueKind == JsonValueKind.String, () => rating.Url.GetString() ?? string.Empty)
			.Map(url => url.Trim('/'))
			.When(url => url.Length > 0);
		return rating.Source switch
		{
			"tomatoes" or "popcorn" => path.Map(found => (IExternalMediaId)RottenTomatoesId.From(found, type)),
			"metacritic" => path.Map(found => (IExternalMediaId)MetacriticId.From(found, type)),
			"letterboxd" => path.Map(found => (IExternalMediaId)LetterboxdId.From(found.Split('/')[^1], type)),
			_ => new None(),
		};
	}
}
