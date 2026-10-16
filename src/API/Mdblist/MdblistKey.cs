using System.Globalization;
using System.Text.Json.Nodes;
using API.Mdblist.Types;
using Domain.Media;
using Domain.Providers;

namespace API.Mdblist;

internal readonly record struct MdblistKey(MediaProvider Source, MediaType Type, string Value)
{
	private static readonly MediaProvider[] AskedFirst =
	[
		MediaProvider.Mdblist,
		MediaProvider.Imdb,
		MediaProvider.Tmdb,
		MediaProvider.Tvdb,
		MediaProvider.Trakt,
		MediaProvider.Mal,
	];

	internal static IReadOnlyList<MediaProvider> Order => AskedFirst;

	internal string Batch => $"{Source}/{Type}";

	internal string Endpoint => string.Format(CultureInfo.InvariantCulture, MdblistEndpoints.Titles, $"{Source}".ToLowerInvariant(), TypeSegment);

	internal static Optional<MdblistKey> Of(IExternalMediaId id)
	{
		return Optional.When(AskedFirst.Contains(id.Source), () => new MdblistKey(id.Source, id.Type, id.Value));
	}

	internal static IEnumerable<MdblistKey> All(MdblistTypes.Titles.Title title, MediaType type) => MdblistIds.Of(title.Ids, type).Select(Of).WhereSome();

	internal bool Outranks(MdblistKey other)
	{
		return Array.IndexOf(AskedFirst, Source) < Array.IndexOf(AskedFirst, other.Source);
	}

	internal JsonNode Encoded()
	{
		return Source is MediaProvider.Imdb or MediaProvider.Mdblist || !long.TryParse(Value, CultureInfo.InvariantCulture, out var number)
			? JsonValue.Create(Value)
			: JsonValue.Create(number);
	}

	private string TypeSegment
	{
		get
		{
			return Type switch
			{
				MediaType.Movie => "movie",
				MediaType.Show => "show",
				MediaType.Unknown => "any",
			};
		}
	}
}
