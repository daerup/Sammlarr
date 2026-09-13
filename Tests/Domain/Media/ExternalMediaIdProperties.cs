using CsCheck;
using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class ExternalMediaIdProperties
{
	private static readonly Gen<MediaProvider> Providers = Gen.OneOfConst(Enum.GetValues<MediaProvider>());

	private static readonly Gen<MediaType> Types = Gen.OneOfConst(Enum.GetValues<MediaType>());

	private static readonly Gen<string> Values = Gen.String[Gen.Char.AlphaNumeric, 1, 12];

	[Test]
	public void Equality_WhenSameProviderValueAndType_HoldsThroughTheInterface() =>
		Gen.Select(Providers, Values, Types)
			.Sample(
				(provider, value, type) =>
				{
					var left = Create(provider, value, type);
					var right = Create(provider, value, type);
					return left.Equals(right) && left.GetHashCode() == right.GetHashCode() && ValueSet.Create([left, right]).Count == 1;
				}
			);

	[Test]
	public void Equality_WhenProvidersDiffer_IsNeverEqual() =>
		Gen.Select(Providers, Providers, Values, Types)
			.Sample(
				(first, second, value, type) =>
				{
					var left = Create(first, value, type);
					var right = Create(second, value, type);
					return first == second ? left.Equals(right) : !left.Equals(right) && ValueSet.Create([left, right]).Count == 2;
				}
			);

	[Test]
	public void Equality_WhenTypesDiffer_IsNeverEqual() =>
		Gen.Select(Providers, Values, Types, Types)
			.Sample(
				(provider, value, first, second) =>
				{
					var left = Create(provider, value, first);
					var right = Create(provider, value, second);
					return first == second ? left.Equals(right) : !left.Equals(right);
				}
			);

	private static IExternalMediaId Create(MediaProvider provider, string value, MediaType type) =>
		provider switch
		{
			MediaProvider.Tmdb => TmdbId.From(value, type),
			MediaProvider.Imdb => ImdbId.From(value, type),
			MediaProvider.Tvdb => TvdbId.From(value, type),
			MediaProvider.Plex => PlexId.From(value, type),
			MediaProvider.Mdblist => MdblistId.From(value, type),
		};
}
