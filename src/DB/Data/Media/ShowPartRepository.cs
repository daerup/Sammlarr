using System.Text.Json;
using Contracts;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shared.Options;

namespace DB.Data.Media;

internal sealed class ShowPartRepository(IDbContextFactory<SammlarrDbContext> contexts, IDateTimeProvider clock, HousekeepingOptions options) : IShowParts
{
	private static readonly DateTimeOffsetToBinaryConverter BinaryTime = new();

	public async Task<IReadOnlyDictionary<IExternalMediaId, Optional<IReadOnlyList<IExternalMediaId>>>> GetShowsAsync(
		IReadOnlyCollection<IExternalMediaId> parts,
		CancellationToken cancellationToken = default
	)
	{
		List<string> ids = [.. parts.Select(part => part.Value).Distinct()];
		var askedSince = (clock.Now - options.NoPrequelFor).ToUniversalTime();
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var answered = await (
			from part in ctx.ShowParts.AsNoTracking()
			join key in ctx.ExternalMediaIds.AsNoTracking()
				on EF.Property<Guid?>(part, MediaQueries.MediaIdColumn) equals (Guid?)EF.Property<Guid>(key, MediaQueries.MediaIdColumn)
				into keys
			from key in keys.DefaultIfEmpty()
			where ids.Contains(part.Id)
			where key != null || (EF.Property<Guid?>(part, MediaQueries.MediaIdColumn) == null && part.AskedAt > askedSince)
			select new Answered(part.Source, part.Id, key)
		).ToListAsync(cancellationToken);
		var shows = answered.ToLookup(row => (row.Source, row.Id), row => Optional.Of(row.Key).Map(key => key.ToExternalMediaId()));
		return parts
			.Distinct()
			.Where(asked => shows.Contains((asked.Source, asked.Value)))
			.ToDictionary(asked => asked, asked => ShowOf(shows[(asked.Source, asked.Value)]));
	}

	public async Task AddAsync(
		IReadOnlyCollection<IExternalMediaId> parts,
		Optional<MediaId> show,
		ShowPartPlace place,
		CancellationToken cancellationToken = default
	)
	{
		var rows = JsonSerializer.Serialize(parts.Distinct().Select(part => new[] { $"{part.Source}", part.Value }));
		var media = show.Map(found => found.Value).ToNullable();
		var season = place.TvdbSeason.ToNullable();
		var offset = place.EpisodeOffset.ToNullable();
		var askedAt = BinaryTime.ConvertToProvider(clock.Now.ToUniversalTime());
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		await ctx.Database.ExecuteSqlAsync(
			$"""
			INSERT INTO "ShowParts" ("Source", "Id", "MediaId", "TvdbSeason", "EpisodeOffset", "AskedAt")
			SELECT json_extract(p.value, '$[0]'), json_extract(p.value, '$[1]'), {media}, {season}, {offset}, {askedAt}
			FROM json_each({rows}) AS p
			WHERE {media} IS NULL OR EXISTS (SELECT 1 FROM "Media" WHERE "Id" = {media})
			ON CONFLICT ("Id", "Source") DO UPDATE SET
			  "MediaId" = excluded."MediaId", "TvdbSeason" = excluded."TvdbSeason", "EpisodeOffset" = excluded."EpisodeOffset", "AskedAt" = excluded."AskedAt"
			WHERE excluded."MediaId" IS NOT NULL OR "ShowParts"."MediaId" IS NULL
			""",
			cancellationToken
		);
	}

	private static Optional<IReadOnlyList<IExternalMediaId>> ShowOf(IEnumerable<Optional<IExternalMediaId>> keys)
	{
		List<IExternalMediaId> found = [.. keys.WhereSome()];
		return Optional.When(found.Count > 0, () => (IReadOnlyList<IExternalMediaId>)found);
	}

	private sealed record Answered(MediaProvider Source, string Id, ExternalMediaIdRow? Key);
}
