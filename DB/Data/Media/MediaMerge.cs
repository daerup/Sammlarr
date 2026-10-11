using Contracts.Media;
using DB.Data.Feed;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaMerge(IDbContextFactory<SammlarrDbContext> contexts, MediaWriteLock writeLock) : IMediaMerge
{
	public async Task<MergeOutcome> MergeAsync(MediaId held, MediaId other, CancellationToken cancellationToken = default)
	{
		using var _ = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		var rows = await ctx
			.Media.AsNoTracking()
			.Include(row => row.ExternalIds)
			.Where(row => row.Id == held.Value || row.Id == other.Value)
			.ToListAsync(cancellationToken);
		if (rows.Count < 2)
		{
			return new NotStored();
		}

		var outcome = await IdentityConflict
			.Contradicts(Keys(rows[0]), Keys(rows[1]))
			.Match<DistinctReason, MergeOutcome>(reason => new Contradicted(reason), () => MoveAsync(ctx, rows, cancellationToken));
		await transaction.CommitAsync(cancellationToken);
		return outcome;
	}

	private static ValueSet<IIdentityKey> Keys(MediaRow row)
	{
		return [.. row.ExternalIds.Select(id => (IIdentityKey)id.ToExternalMediaId())];
	}

	private static async Task<MergeOutcome> MoveAsync(SammlarrDbContext ctx, IReadOnlyList<MediaRow> rows, CancellationToken cancellationToken)
	{
		List<Guid> ids = [.. rows.Select(row => row.Id)];
		var listed = await ctx
			.Wantlist.IgnoreQueryFilters()
			.Select(wanted => wanted.Media.Id)
			.Union(ctx.Blocklist.IgnoreQueryFilters().Select(blocked => blocked.Media.Id))
			.Where(id => ids.Contains(id))
			.ToListAsync(cancellationToken);
		List<MediaRow> ordered = [.. rows.OrderBy(row => !listed.Contains(row.Id)).ThenBy(row => row.CreatedAt)];
		var (keep, gone) = (ordered[0].Id, ordered[1].Id);
		await ctx.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys = ON", cancellationToken);
		await RehomeAsync(Of(ctx.ExternalMediaIds, gone), keep, cancellationToken);
		await MoveDetailsAsync(ctx, keep, gone, cancellationToken);
		await MoveListedAsync(ctx.Wantlist, keep, gone, cancellationToken);
		await MoveListedAsync(ctx.Blocklist, keep, gone, cancellationToken);
		await MoveOutboxAsync(ctx, keep, gone, cancellationToken);
		await MoveRunItemsAsync(ctx, keep, gone, cancellationToken);
		await MoveAbsencesAsync(ctx, keep, gone, cancellationToken);
		await ctx.Media.Where(media => media.Id == gone).ExecuteDeleteAsync(cancellationToken);
		return new Merged(MediaId.From(keep), MediaId.From(gone));
	}

	private static async Task MoveDetailsAsync(SammlarrDbContext ctx, Guid keep, Guid gone, CancellationToken cancellationToken)
	{
		var sources = Of(ctx.MediaRatings, keep).Select(rating => rating.Source);
		await RehomeAsync(Of(ctx.MediaRatings, gone).Where(rating => !sources.Contains(rating.Source)), keep, cancellationToken);
		var sites = Of(ctx.Set<MediaPageLinkRow>(), keep).Select(link => link.Site);
		await RehomeAsync(Of(ctx.Set<MediaPageLinkRow>(), gone).Where(link => !sites.Contains(link.Site)), keep, cancellationToken);
		var providers = Of(ctx.ProviderAnswers, keep).Select(answer => answer.Provider);
		await RehomeAsync(Of(ctx.ProviderAnswers, gone).Where(answer => !providers.Contains(answer.Provider)), keep, cancellationToken);
		var kept = await ctx.MediaDetails.SingleOrDefaultAsync(details => details.MediaId == keep, cancellationToken);
		var removed = await ctx.MediaDetails.AsNoTracking().SingleOrDefaultAsync(details => details.MediaId == gone, cancellationToken);
		await Optional
			.Of(removed)
			.Apply(other =>
				Optional
					.Of(kept)
					.Match(survivor => FillAsync(ctx, survivor, other, cancellationToken), () => RehomeAsync(Of(ctx.MediaDetails, gone), keep, cancellationToken))
			);
	}

	private static async Task FillAsync(SammlarrDbContext ctx, MediaDetailsRow survivor, MediaDetailsRow other, CancellationToken cancellationToken)
	{
		survivor.Title = Filled(survivor.Title, other.Title);
		survivor.Tagline = Filled(survivor.Tagline, other.Tagline);
		survivor.Summary = Filled(survivor.Summary, other.Summary);
		survivor.PosterUrl = Filled(survivor.PosterUrl, other.PosterUrl);
		survivor.BackdropUrl = Filled(survivor.BackdropUrl, other.BackdropUrl);
		survivor.ReleaseDate ??= other.ReleaseDate;
		survivor.Genres = survivor.Genres is [] ? other.Genres : survivor.Genres;
		survivor.RefreshedAt = Earlier(survivor.RefreshedAt, other.RefreshedAt);
		survivor.AskedAt = Earlier(survivor.AskedAt, other.AskedAt);
		FillLength(survivor, other);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static void FillLength(MediaDetailsRow survivor, MediaDetailsRow other)
	{
		switch (survivor, other)
		{
			case (MovieDetailsRow movie, MovieDetailsRow removed):
				movie.Runtime ??= removed.Runtime;
				break;
			case (ShowDetailsRow show, ShowDetailsRow removed):
				show.Seasons ??= removed.Seasons;
				show.Episodes ??= removed.Episodes;
				show.RuntimePerEpisode ??= removed.RuntimePerEpisode;
				break;
		}
	}

	private static string? Filled(string? kept, string? other) => string.IsNullOrEmpty(kept) ? other : kept;

	private static DateTimeOffset Earlier(DateTimeOffset kept, DateTimeOffset other) => kept < other ? kept : other;

	private static async Task MoveListedAsync<TRow>(IQueryable<TRow> table, Guid keep, Guid gone, CancellationToken cancellationToken)
		where TRow : class, IAuditable
	{
		var rows = table.IgnoreQueryFilters();
		var listed = await Of(rows, keep)
			.Concat(Of(rows, gone))
			.Select(row => new Listed(
				EF.Property<Guid>(row, UserOwnedConfigurationExtensions.UserIdColumn),
				EF.Property<Guid>(row, MediaQueries.MediaIdColumn),
				row.CreatedAt
			))
			.ToListAsync(cancellationToken);
		var newer = listed
			.GroupBy(entry => entry.User)
			.Where(user => user.Count() > 1)
			.Select(user => user.OrderBy(entry => entry.At).ThenBy(entry => entry.Media != keep).Last());
		await DropAsync(rows, newer.Select(entry => (entry.User, entry.Media)), cancellationToken);
		await RehomeAsync(Of(rows, gone), keep, cancellationToken);
	}

	private static async Task MoveOutboxAsync(SammlarrDbContext ctx, Guid keep, Guid gone, CancellationToken cancellationToken)
	{
		var rows = ctx.WantlistOutbox.IgnoreQueryFilters();
		var wanting = await Of(ctx.Wantlist.IgnoreQueryFilters(), keep)
			.Select(row => EF.Property<Guid>(row, UserOwnedConfigurationExtensions.UserIdColumn))
			.ToListAsync(cancellationToken);
		var queued = await Of(rows, keep)
			.Concat(Of(rows, gone))
			.Select(row => new Queued(
				EF.Property<Guid>(row, UserOwnedConfigurationExtensions.UserIdColumn),
				EF.Property<Guid>(row, MediaQueries.MediaIdColumn),
				row.Action
			))
			.ToListAsync(cancellationToken);
		var disagreeing = queued
			.GroupBy(entry => entry.User)
			.Where(user => user.Count() > 1)
			.Select(user => user.OrderBy(entry => entry.Action == Agreeing(wanting.Contains(user.Key))).ThenBy(entry => entry.Media == keep).First());
		await DropAsync(rows, disagreeing.Select(entry => (entry.User, entry.Media)), cancellationToken);
		await RehomeAsync(Of(rows, gone), keep, cancellationToken);
	}

	private static WantlistOperationAction Agreeing(bool wanted) => wanted ? WantlistOperationAction.Add : WantlistOperationAction.Remove;

	private static async Task DropAsync<TRow>(IQueryable<TRow> rows, IEnumerable<(Guid User, Guid Media)> entries, CancellationToken cancellationToken)
		where TRow : class
	{
		foreach (var (user, media) in entries)
		{
			await Of(rows, media).Where(row => EF.Property<Guid>(row, UserOwnedConfigurationExtensions.UserIdColumn) == user).ExecuteDeleteAsync(cancellationToken);
		}
	}

	private static async Task MoveRunItemsAsync(SammlarrDbContext ctx, Guid keep, Guid gone, CancellationToken cancellationToken)
	{
		var items = ctx.FeedRunItems.IgnoreQueryFilters();
		var runs = Of(items, keep).Select(item => item.FeedRunId);
		await Of(items, gone).Where(item => runs.Contains(item.FeedRunId)).ExecuteDeleteAsync(cancellationToken);
		await RehomeAsync(Of(items, gone), keep, cancellationToken);
	}

	private static async Task MoveAbsencesAsync(SammlarrDbContext ctx, Guid keep, Guid gone, CancellationToken cancellationToken)
	{
		var absences = ctx.FeedAbsences.IgnoreQueryFilters();
		var missed = await Of(absences, keep).Concat(Of(absences, gone)).AsNoTracking().ToListAsync(cancellationToken);
		var seenSince = missed.GroupBy(absence => absence.FeedId).SelectMany(SeenSince);
		foreach (var absence in seenSince)
		{
			await Of(absences, absence.MediaId).Where(row => row.FeedId == absence.FeedId).ExecuteDeleteAsync(cancellationToken);
		}

		await RehomeAsync(Of(absences, gone), keep, cancellationToken);
	}

	private static IEnumerable<FeedAbsenceRow> SeenSince(IGrouping<Guid, FeedAbsenceRow> feed)
	{
		return feed.Count() == 1 ? feed : feed.OrderBy(absence => absence.MissedRuns).Skip(1);
	}

	private static IQueryable<TRow> Of<TRow>(IQueryable<TRow> rows, Guid media)
		where TRow : class
	{
		return rows.Where(row => EF.Property<Guid>(row, MediaQueries.MediaIdColumn) == media);
	}

	private static async Task RehomeAsync<TRow>(IQueryable<TRow> rows, Guid keep, CancellationToken cancellationToken)
		where TRow : class
	{
		await rows.ExecuteUpdateAsync(setters => setters.SetProperty(row => EF.Property<Guid>(row, MediaQueries.MediaIdColumn), keep), cancellationToken);
	}

	private sealed record Listed(Guid User, Guid Media, DateTimeOffset At);

	private sealed record Queued(Guid User, Guid Media, WantlistOperationAction Action);
}
