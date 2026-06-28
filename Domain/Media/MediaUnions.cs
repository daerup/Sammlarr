namespace Domain.Media;

public static class MediaUnions
{
	extension(MediaMatch media)
	{
		public MediaIdentity Identity =>
			media switch
			{
				IMedia resolved => resolved.Identity,
				IPartialMedia partial => partial.Identity,
				UnmatchedMedia => throw new InvalidOperationException("Unmatched media has no identity."),
			};

		public ValueSet<IExternalMediaId> Externals =>
			media switch
			{
				IMedia resolved => resolved.Identity.Externals,
				IPartialMedia partial => partial.Identity.Externals,
				UnmatchedMedia unmatched => unmatched.Externals,
			};

		public MediaMatch CombineIdentity(MediaIdentity identity)
		{
			var combined = identity with { Externals = identity.Externals + media.Externals };
			return media switch
			{
				Movie movie => movie with { Identity = combined },
				Show show => show with { Identity = combined },
				PartialMovie movie => movie with { Identity = combined },
				PartialShow show => show with { Identity = combined },
				UnmatchedMedia unmatched => Identify(unmatched, combined),
				_ => media,
			};
		}
	}

	private static MediaMatch Identify(UnmatchedMedia unmatched, MediaIdentity identity) =>
		identity.Externals.FirstOrDefault()?.Type switch
		{
			MediaType.Movie => new PartialMovie
			{
				Identity = identity,
				Title = unmatched.Title.NullIfBlank(),
				Summary = unmatched.Summary.NullIfBlank(),
				PosterUrl = unmatched.PosterUrl.NullIfBlank(),
				BackdropUrl = unmatched.BackdropUrl.NullIfBlank(),
				ReleaseDate = unmatched.ReleaseDate,
				Genres = unmatched.Genres,
				Rating = unmatched.Rating,
			},
			MediaType.Show => new PartialShow
			{
				Identity = identity,
				Title = unmatched.Title.NullIfBlank(),
				Summary = unmatched.Summary.NullIfBlank(),
				PosterUrl = unmatched.PosterUrl.NullIfBlank(),
				BackdropUrl = unmatched.BackdropUrl.NullIfBlank(),
				ReleaseDate = unmatched.ReleaseDate,
				Genres = unmatched.Genres,
				Rating = unmatched.Rating,
			},
			_ => unmatched with { Externals = unmatched.Externals + identity.Externals },
		};
}
