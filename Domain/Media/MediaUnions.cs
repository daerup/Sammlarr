using Domain.Media.Metadata;

namespace Domain.Media;

public static class MediaUnions
{
	extension(IdentifiedMedia media)
	{
		public MediaId Id => media.Identity.Id;

		public ValueSet<IExternalMediaId> Externals => media.Identity.Externals;

		public MediaType Type => media.Details.Type;

		public string? Title => media.Details.Title;

		public string? Tagline => media.Details.Tagline;

		public string? Summary => media.Details.Summary;

		public string? PosterUrl => media.Details.PosterUrl;

		public string? BackdropUrl => media.Details.BackdropUrl;

		public DateOnly? ReleaseDate => media.Details.ReleaseDate;

		public RatingSummary Rating => media.Details.Rating;

		public ValueSet<string> Genres => media.Details.Genres;

		public int? Seasons => media.Details is ShowDetails show ? show.Seasons : null;

		public int? Episodes => media.Details is ShowDetails show ? show.Episodes : null;

		public TimeSpan? Runtime => media.Details is MovieDetails movie ? movie.Runtime : null;

		public TimeSpan? RuntimePerEpisode => media.Details is ShowDetails show ? show.RuntimePerEpisode : null;

		public MediaLookup ToMatch() => media;
	}

	extension(MediaLookup media)
	{
		public bool IsUnmatched => media is UnmatchedMedia;

		public MediaIdentity Identity =>
			media switch
			{
				IdentifiedMedia identified => identified.Identity,
				UnmatchedMedia => throw new InvalidOperationException("Unmatched media has no identity."),
			};

		public ValueSet<IExternalMediaId> Externals
		{
			get
			{
				return media switch
				{
					IdentifiedMedia identified => identified.Identity.Externals,
					UnmatchedMedia unmatched => unmatched.Externals,
				};
			}
		}

		public IdentifiedMedia? AsIdentified()
		{
			return media switch
			{
				IdentifiedMedia identified => identified,
				UnmatchedMedia => null,
			};
		}

		public Optional<IdentifiedMedia> ToIdentified() =>
			media switch
			{
				IdentifiedMedia identified => Some.Of(identified),
				UnmatchedMedia => new None(),
			};

		public MediaLookup CombineIdentity(MediaIdentity identity)
		{
			var combined = identity with { Externals = identity.Externals + media.Externals };
			return media switch
			{
				IdentifiedMedia identified => identified with { Identity = combined },
				UnmatchedMedia unmatched => Identify(unmatched, combined),
			};
		}
	}

	private static MediaLookup Identify(UnmatchedMedia unmatched, MediaIdentity identity)
	{
		return identity.Externals.FirstOrDefault()?.Type switch
		{
			MediaType.Movie => new IdentifiedMedia(identity, Retyped(unmatched.Details, MediaType.Movie)),
			MediaType.Show => new IdentifiedMedia(identity, Retyped(unmatched.Details, MediaType.Show)),
			_ => unmatched with { Externals = unmatched.Externals + identity.Externals },
		};
	}

	private static MediaDetails Retyped(MediaDetails details, MediaType type)
	{
		return (details.Type == type ? details : MediaDetails.Empty(type)) with
		{
			Title = details.Title.NullIfBlank(),
			Tagline = details.Tagline.NullIfBlank(),
			Summary = details.Summary.NullIfBlank(),
			PosterUrl = details.PosterUrl.NullIfBlank(),
			BackdropUrl = details.BackdropUrl.NullIfBlank(),
			ReleaseDate = details.ReleaseDate,
			Genres = details.Genres,
			Rating = details.Rating,
		};
	}
}
