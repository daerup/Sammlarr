using CsCheck;
using Domain.Media;
using Domain.Media.Metadata;

namespace Domain.Tests.Media.Metadata;

public class MediaMergerProperties
{
	private static readonly Gen<PartialMovie> Movies = Gen.Bool.Array[12].Select(Build);

	[Test]
	public void Merging_two_partials_keeps_every_external_id_from_both() =>
		Gen.Select(Movies, Movies)
			.Sample(
				(primary, secondary) =>
				{
					var merged = ((IPartialMedia)primary).MergeWith(secondary);
					return merged.Externals.IsSupersetOf(primary.Identity.Externals) && merged.Externals.IsSupersetOf(secondary.Identity.Externals);
				}
			);

	[Test]
	public void Merging_two_partials_only_fills_gaps_and_never_removes_data() =>
		Gen.Select(Movies, Movies)
			.Sample(
				(primary, secondary) =>
					((IPartialMedia)primary).MergeWith(secondary) is not IPartialMedia merged || merged.MissingFields.All(primary.MissingFields.Contains)
			);

	private static PartialMovie Build(bool[] flags) =>
		new()
		{
			Identity = new MediaIdentity(default, Externals(flags)),
			Title = flags[4] ? "Title" : null,
			Tagline = flags[5] ? "Tagline" : null,
			Summary = flags[6] ? "Summary" : null,
			PosterUrl = flags[7] ? "/poster.jpg" : null,
			BackdropUrl = flags[8] ? "/backdrop.jpg" : null,
			ReleaseDate = flags[9] ? new DateOnly(2020, 1, 1) : null,
			Genres = flags[10] ? ["Drama"] : [],
			Runtime = flags[11] ? TimeSpan.FromHours(2) : null,
			Rating = RatingSummary.None,
		};

	private static ValueSet<IExternalMediaId> Externals(bool[] flags)
	{
		var ids = new List<IExternalMediaId>();
		if (flags[0])
		{
			ids.Add(TmdbId.From("1", MediaType.Movie));
		}

		if (flags[1])
		{
			ids.Add(TmdbId.From("2", MediaType.Movie));
		}

		if (flags[2])
		{
			ids.Add(PlexId.From("1", MediaType.Movie));
		}

		if (flags[3])
		{
			ids.Add(ImdbId.From("1", MediaType.Movie));
		}

		return [.. ids];
	}
}
