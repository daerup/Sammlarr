using Contracts;
using Contracts.Media;
using DB.Data.Media;
using DB.Data.Metadata;
using Domain.Media;
using Integration.Tests.Infrastructure.Hosting;

namespace Integration.Tests.Infrastructure.Media.Local;

public sealed class MediaSeeder(SammlarrScenarioContext sammlarr)
{
	public Task<MediaId> SeedIdentityAsync(ResolvedVersion resolved) => Seed(resolved, fetched: null, storeMetadata: false);

	public Task<MediaId> SeedCachedAsync(ResolvedVersion resolved, DateTimeOffset? fetched) => Seed(resolved, fetched, storeMetadata: true);

	private async Task<MediaId> Seed(ResolvedVersion resolved, DateTimeOffset? fetched, bool storeMetadata)
	{
		var ctx = sammlarr.DbContext;
		var media = new MediaRow();
		foreach (var (provider, id) in resolved.Ids)
			await ctx.ExternalMediaIds.AddAsync(ExternalIdFactory.Create(provider, id, resolved.Type).ToExternalIdEntity(media));
		await ctx.SaveChangesAsync();
		var mediaId = MediaId.From(media.Id);

		if (storeMetadata)
		{
			await ctx.MediaMetadata.AddAsync(BuildMetadata(media.Id, resolved, fetched));
			await ctx.SaveChangesAsync();
		}

		ctx.ChangeTracker.Clear();
		return mediaId;
	}

	private MediaMetadataRow BuildMetadata(Guid mediaId, ResolvedVersion resolved, DateTimeOffset? fetched)
	{
		MediaMetadataRow metadata =
			resolved.Type == MediaType.Show
				? new ShowMetadataRow
				{
					Seasons = 1,
					Episodes = 1,
					RuntimePerEpisode = TimeSpan.FromMinutes(30),
				}
				: new MovieMetadataRow { Runtime = TimeSpan.FromHours(2) };

		metadata.MediaId = mediaId;
		metadata.Title = resolved.Title;
		metadata.Tagline = "Tagline";
		metadata.Summary = resolved.Summary ?? "An overview";
		metadata.PosterUrl = resolved.Poster ?? "/poster.jpg";
		metadata.BackdropUrl = "/backdrop.jpg";
		metadata.ReleaseDate = new DateOnly(resolved.Year, 1, 15);
		metadata.Genres = resolved.Genres is null ? ["Action"] : [.. resolved.Genres];
		metadata.LastFetchedAt = fetched ?? sammlarr.Resolve<IDateTimeProvider>().Now;
		return metadata;
	}
}
