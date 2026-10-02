using Domain.Media.Metadata;

namespace Domain.Media;

public static class MediaUnions
{
	extension(IMedia media)
	{
		public MediaLookup ToMatch() => (IdentifiedMedia)media;
	}

	extension(IPartialMedia media)
	{
		public MediaLookup ToMatch() => (IdentifiedMedia)media;
	}

	extension(IdentifiedMedia media)
	{
		public MediaIdentity Identity =>
			media switch
			{
				IMedia resolved => resolved.Identity,
				IPartialMedia partial => partial.Identity,
			};

		public MediaId Id => media.Identity.Id;

		public ValueSet<IExternalMediaId> Externals =>
			media switch
			{
				IMedia resolved => resolved.Identity.Externals,
				IPartialMedia partial => partial.Identity.Externals,
			};

		public MediaType Type =>
			media switch
			{
				IMedia resolved => resolved.Type,
				IPartialMedia partial => partial.Type,
			};

		public string? Title =>
			media switch
			{
				IMedia resolved => resolved.Title,
				IPartialMedia partial => partial.Title,
			};

		public string? Tagline =>
			media switch
			{
				IMedia resolved => resolved.Tagline,
				IPartialMedia partial => partial.Tagline,
			};

		public string? Summary =>
			media switch
			{
				IMedia resolved => resolved.Summary,
				IPartialMedia partial => partial.Summary,
			};

		public string? PosterUrl =>
			media switch
			{
				IMedia resolved => resolved.PosterUrl,
				IPartialMedia partial => partial.PosterUrl,
			};

		public string? BackdropUrl =>
			media switch
			{
				IMedia resolved => resolved.BackdropUrl,
				IPartialMedia partial => partial.BackdropUrl,
			};

		public DateOnly? ReleaseDate =>
			media switch
			{
				IMedia resolved => resolved.ReleaseDate,
				IPartialMedia partial => partial.ReleaseDate,
			};

		public RatingSummary Rating =>
			media switch
			{
				IMedia resolved => resolved.Rating,
				IPartialMedia partial => partial.Rating,
			};

		public ValueSet<string> Genres =>
			media switch
			{
				IMedia resolved => resolved.Genres,
				IPartialMedia partial => partial.Genres,
			};

		public int? Seasons =>
			media switch
			{
				Show show => show.Seasons,
				PartialShow show => show.Seasons,
				_ => null,
			};

		public int? Episodes =>
			media switch
			{
				Show show => show.Episodes,
				PartialShow show => show.Episodes,
				_ => null,
			};

		public TimeSpan? Runtime =>
			media switch
			{
				Movie movie => movie.Runtime,
				PartialMovie movie => movie.Runtime,
				_ => null,
			};

		public TimeSpan? RuntimePerEpisode =>
			media switch
			{
				Show show => show.RuntimePerEpisode,
				PartialShow show => show.RuntimePerEpisode,
				_ => null,
			};

		public IdentifiedMedia WithIdentity(MediaIdentity identity) =>
			media switch
			{
				Movie movie => movie with { Identity = identity },
				Show show => show with { Identity = identity },
				PartialMovie movie => movie with { Identity = identity },
				PartialShow show => show with { Identity = identity },
				_ => media,
			};

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

		public ValueSet<IExternalMediaId> Externals =>
			media switch
			{
				IdentifiedMedia identified => identified.Externals,
				UnmatchedMedia unmatched => unmatched.Externals,
			};

		public IdentifiedMedia? AsIdentified() =>
			media switch
			{
				IdentifiedMedia identified => (IdentifiedMedia?)identified,
				UnmatchedMedia => null,
			};

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
				IdentifiedMedia identified => identified.WithIdentity(combined),
				UnmatchedMedia unmatched => Identify(unmatched, combined),
			};
		}
	}

	private static MediaLookup Identify(UnmatchedMedia unmatched, MediaIdentity identity) =>
		identity.Externals.FirstOrDefault()?.Type switch
		{
			MediaType.Movie => IdentifiedMedia.From(
				new PartialMovie
				{
					Identity = identity,
					Title = unmatched.Title.NullIfBlank(),
					Summary = unmatched.Summary.NullIfBlank(),
					PosterUrl = unmatched.PosterUrl.NullIfBlank(),
					BackdropUrl = unmatched.BackdropUrl.NullIfBlank(),
					ReleaseDate = unmatched.ReleaseDate,
					Genres = unmatched.Genres,
					Rating = unmatched.Rating,
				}
			),
			MediaType.Show => IdentifiedMedia.From(
				new PartialShow
				{
					Identity = identity,
					Title = unmatched.Title.NullIfBlank(),
					Summary = unmatched.Summary.NullIfBlank(),
					PosterUrl = unmatched.PosterUrl.NullIfBlank(),
					BackdropUrl = unmatched.BackdropUrl.NullIfBlank(),
					ReleaseDate = unmatched.ReleaseDate,
					Genres = unmatched.Genres,
					Rating = unmatched.Rating,
				}
			),
			_ => unmatched with { Externals = unmatched.Externals + identity.Externals },
		};
}
