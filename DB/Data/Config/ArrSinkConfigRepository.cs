using Contracts.Settings;
using Domain.Wantlist.Arr;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class ArrSinkConfigRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IArrSinkConfigRepository
{
	public async Task<ArrSinkConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken) ?? ProviderConfigRow.Default;
		return new ArrSinkConfig(
			new ArrInstanceConfig
			{
				Url = Optional.Of(row.RadarrUrl),
				ApiKey = Optional.Of(row.RadarrApiKey),
				QualityProfileId = Optional.Of(row.RadarrQualityProfileId),
				RootFolderPath = Optional.Of(row.RadarrRootFolderPath),
				Monitored = row.RadarrMonitored,
				SearchOnAdd = row.RadarrSearchOnAdd,
			},
			new ArrInstanceConfig
			{
				Url = Optional.Of(row.SonarrUrl),
				ApiKey = Optional.Of(row.SonarrApiKey),
				QualityProfileId = Optional.Of(row.SonarrQualityProfileId),
				RootFolderPath = Optional.Of(row.SonarrRootFolderPath),
				Monitored = row.SonarrMonitored,
				SearchOnAdd = row.SonarrSearchOnAdd,
			},
			new ArrTagScheme(row.ArrTagPrefix, row.ArrTagPerUser, row.ArrTagUserSource, row.ArrTagPerFeed, row.ArrTagFeedSource, row.ArrTagPlainPrefix)
		);
	}

	public async Task StoreAsync(ArrSinkConfig config, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var stored = await ctx.ProviderSettings.SingleOrDefaultAsync(cancellationToken);
		var row = stored ?? ctx.ProviderSettings.Add(ProviderConfigRow.Default).Entity;
		row.RadarrUrl = config.Radarr.Url.ToNullable();
		row.RadarrApiKey = config.Radarr.ApiKey.ToNullable();
		row.RadarrQualityProfileId = config.Radarr.QualityProfileId.ToNullable();
		row.RadarrRootFolderPath = config.Radarr.RootFolderPath.ToNullable();
		row.RadarrMonitored = config.Radarr.Monitored;
		row.RadarrSearchOnAdd = config.Radarr.SearchOnAdd;
		row.SonarrUrl = config.Sonarr.Url.ToNullable();
		row.SonarrApiKey = config.Sonarr.ApiKey.ToNullable();
		row.SonarrQualityProfileId = config.Sonarr.QualityProfileId.ToNullable();
		row.SonarrRootFolderPath = config.Sonarr.RootFolderPath.ToNullable();
		row.SonarrMonitored = config.Sonarr.Monitored;
		row.SonarrSearchOnAdd = config.Sonarr.SearchOnAdd;
		row.ArrTagPrefix = config.Tags.Prefix;
		row.ArrTagPerUser = config.Tags.PerUser;
		row.ArrTagUserSource = config.Tags.UserSource;
		row.ArrTagPerFeed = config.Tags.PerFeed;
		row.ArrTagFeedSource = config.Tags.FeedSource;
		row.ArrTagPlainPrefix = config.Tags.PlainPrefix;
		await ctx.SaveChangesAsync(cancellationToken);
	}
}
