using System.Globalization;
using System.Text.Json;
using API.Mdblist.Types;
using Contracts.Media.Conversions;
using Domain.Media;

namespace API.Mdblist;

internal static class MdblistIds
{
	internal static IReadOnlyList<IExternalMediaId> Of(MdblistTypes.ListItems.Ids ids, MediaType type)
	{
		Optional<IExternalMediaId>[] found =
		[
			Text(ids.mdblist).Map(value => Id(MdblistId.From(value, type))),
			Optional.Of(ids.tmdb).Map(value => Id(TmdbId.From(Number(value), type))),
			Text(ids.imdb).Map(value => Id(ImdbId.From(value, type))),
			Optional.Of(ids.tvdb).Map(value => Id(TvdbId.From(Number(value), type))),
			Optional.Of(ids.trakt).Map(value => Id(TraktId.From(Number(value), type))),
			Optional.Of(ids.mal).Map(Mal).Map(value => Id(MalId.From(value, type))),
		];
		return [.. found.WhereSome()];
	}

	internal static IdLookupResult Found(MdblistTypes.Titles.Title title, MediaType type)
	{
		var hints = Text(title.Name)
			.Map(name => new IdentifyHints
			{
				Title = name,
				Year = title.Year ?? 0,
				MediaType = type,
			});
		return new IdLookupResult(Of(title.Ids, type), hints);
	}

	private static Optional<string> Text(string? value) => Optional.Of(value).When(found => found.Length > 0);

	private static Optional<string> Mal(JsonElement value)
	{
		return value.ValueKind switch
		{
			JsonValueKind.Number => Some.Of(value.GetInt64().ToString(CultureInfo.InvariantCulture)),
			JsonValueKind.String => Text(value.GetString()),
			_ => new None(),
		};
	}

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static IExternalMediaId Id(IExternalMediaId id) => id;
}
