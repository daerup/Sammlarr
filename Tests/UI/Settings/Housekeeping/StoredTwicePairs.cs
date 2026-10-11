using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace UI.Tests.Settings.Housekeeping;

internal static class StoredTwicePairs
{
	internal static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	internal static StoredTwice Pair(StoredTitle held, StoredTitle other, ConflictState state, int attempts)
	{
		var note = new IdentityConflict(
			held.Media.Identity.Id,
			other.Media.Identity.Id,
			other.Media.Identity.Keys.First(),
			[RemoteService.Tmdb],
			Now,
			attempts,
			Now.AddDays(attempts),
			new None(),
			state
		);
		return new StoredTwice(note, held, other);
	}

	internal static StoredTitle Title(string? name, int? year, params IIdentityKey[] keys)
	{
		var details = Optional
			.Of(name)
			.Map(title => new CachedDetails(
				new MovieDetails
				{
					Title = title,
					ReleaseDate = Optional.Of(year).Map(released => new DateOnly(released, 5, 1)).ToNullable(),
					Genres = ["Adventure"],
					Runtime = TimeSpan.FromMinutes(150),
					Ratings = MediaRatings.None,
				},
				Now,
				Now,
				[]
			));
		return new StoredTitle(new StoredMedia(new MediaIdentity(MediaId.New(), [.. keys]), details), new TitleUses(1, 0));
	}
}
