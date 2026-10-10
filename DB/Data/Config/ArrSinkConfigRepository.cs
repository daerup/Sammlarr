using Contracts.Settings;
using Domain.Wantlist.Arr;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Config;

internal sealed class ArrSinkConfigRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IArrSinkConfigRepository
{
	public async Task<ArrSinkConfig> GetAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.AsNoTracking().SingleAsync(cancellationToken);
		return new ArrSinkConfig(
			Instance(row.Radarr),
			Instance(row.Sonarr),
			new ArrTagScheme(
				row.ArrTagPrefix,
				row.ArrTagPerUser,
				row.ArrTagUserSource,
				row.ArrTagPerFeed,
				row.ArrTagFeedSource,
				row.ArrTagPlainPrefix,
				row.ArrTagProtection
			)
		);
	}

	public async Task StoreAsync(ArrSinkConfig config, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.ProviderSettings.SingleAsync(cancellationToken);
		Write(row.Radarr, config.Radarr);
		Write(row.Sonarr, config.Sonarr);
		row.ArrTagPrefix = config.Tags.Prefix;
		row.ArrTagPerUser = config.Tags.PerUser;
		row.ArrTagUserSource = config.Tags.UserSource;
		row.ArrTagPerFeed = config.Tags.PerFeed;
		row.ArrTagFeedSource = config.Tags.FeedSource;
		row.ArrTagPlainPrefix = config.Tags.PlainPrefix;
		row.ArrTagProtection = config.Tags.Protection;
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static ArrInstanceConfig Instance(ArrInstanceRow row)
	{
		return new ArrInstanceConfig
		{
			Url = Optional.Of(row.Url),
			ExternalUrl = Optional.Of(row.ExternalUrl),
			ApiKey = Optional.Of(row.ApiKey),
			QualityProfileId = Optional.Of(row.QualityProfileId),
			RootFolderPath = Optional.Of(row.RootFolderPath),
			AnimeQualityProfileId = Optional.Of(row.AnimeQualityProfileId),
			AnimeRootFolderPath = Optional.Of(row.AnimeRootFolderPath),
			Monitored = row.Monitored,
			SearchOnAdd = row.SearchOnAdd,
		};
	}

	private static void Write(ArrInstanceRow row, ArrInstanceConfig instance)
	{
		row.Url = instance.Url.ToNullable();
		row.ExternalUrl = instance.ExternalUrl.ToNullable();
		row.ApiKey = instance.ApiKey.ToNullable();
		row.QualityProfileId = instance.QualityProfileId.ToNullable();
		row.RootFolderPath = instance.RootFolderPath.ToNullable();
		row.AnimeQualityProfileId = instance.AnimeQualityProfileId.ToNullable();
		row.AnimeRootFolderPath = instance.AnimeRootFolderPath.ToNullable();
		row.Monitored = instance.Monitored;
		row.SearchOnAdd = instance.SearchOnAdd;
	}
}
