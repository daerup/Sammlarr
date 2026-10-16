using System.Data.Common;
using System.Text.Json;
using Domain.Media;
using Domain.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal static class MediaQueries
{
	internal const string MediaIdColumn = "MediaId";

	extension(SammlarrDbContext ctx)
	{
		public async Task<DbTransaction> SnapshotAsync(CancellationToken cancellationToken)
		{
			await ctx.Database.OpenConnectionAsync(cancellationToken);
			var snapshot = ((SqliteConnection)ctx.Database.GetDbConnection()).BeginTransaction(deferred: true);
			await ctx.Database.UseTransactionAsync(snapshot, cancellationToken);
			return snapshot;
		}

		public async Task<IReadOnlyList<Guid>> OwnersAsync(IEnumerable<IExternalMediaId> keys, CancellationToken cancellationToken)
		{
			var triples = JsonSerializer.Serialize(keys.Select(key => new[] { $"{key.Source}", $"{key.Type}", key.Value }));
			return await ctx
				.Database.SqlQuery<Guid>(
					$"""
					SELECT DISTINCT e."MediaId" AS "Value"
					FROM json_each({triples}) AS k
					JOIN "ExternalMediaIds" AS e
					  ON e."Source" = json_extract(k.value, '$[0]')
					 AND e."MediaType" = json_extract(k.value, '$[1]')
					 AND e."Id" = json_extract(k.value, '$[2]')
					"""
				)
				.ToListAsync(cancellationToken);
		}

		public async Task<IReadOnlyDictionary<IIdentityKey, MediaId>> KeyOwnersAsync(IReadOnlyList<IIdentityKey> keys, CancellationToken cancellationToken)
		{
			var triples = JsonSerializer.Serialize(keys.Select(key => new[] { $"{key.Source}", $"{key.Type}", key.Value }));
			var owned = await ctx
				.Database.SqlQuery<KeyOwner>(
					$"""
					SELECT e."Source", e."MediaType", e."Id", e."MediaId"
					FROM json_each({triples}) AS k
					JOIN "ExternalMediaIds" AS e
					  ON e."Source" = json_extract(k.value, '$[0]')
					 AND e."MediaType" = json_extract(k.value, '$[1]')
					 AND e."Id" = json_extract(k.value, '$[2]')
					"""
				)
				.ToListAsync(cancellationToken);
			return owned.ToDictionary(
				owner => MediaIdConversionExtensions.Key(Enum.Parse<MediaProvider>(owner.Source), Enum.Parse<MediaType>(owner.MediaType), owner.Id),
				owner => MediaId.From(owner.MediaId)
			);
		}

		public async Task NoteConflictAsync(IdentityConflict note, CancellationToken cancellationToken)
		{
			var (held, other) = (note.Held.Value, note.Other.Value);
			var seen = note.SeenAt.ToUniversalTime();
			var stored = await ctx.IdentityConflicts.SingleOrDefaultAsync(
				row => (row.HeldId == held && row.OtherId == other) || (row.HeldId == other && row.OtherId == held),
				cancellationToken
			);
			Optional
				.Of(stored)
				.Apply(row => row.SeenAt = seen)
				.ApplyNone(() =>
					ctx.IdentityConflicts.Add(
						new IdentityConflictRow
						{
							HeldId = held,
							OtherId = other,
							ClaimedSource = note.Claimed.Source,
							ClaimedType = note.Claimed.Type,
							ClaimedId = note.Claimed.Value,
							SeenAt = seen,
							AskAfter = note.AskAfter.ToUniversalTime(),
							Sources = [.. note.Sources.Select(service => new IdentityConflictSourceRow { Service = service })],
						}
					)
				);
			await ctx.SaveChangesAsync(cancellationToken);
		}

		public IQueryable<Guid> ProtectedMediaIds()
		{
			return ctx
				.Blocklist.IgnoreQueryFilters()
				.Select(blocked => blocked.Media.Id)
				.Union(ctx.Wantlist.IgnoreQueryFilters().Select(wanted => wanted.Media.Id))
				.Union(ctx.WantlistOutbox.IgnoreQueryFilters().Select(queued => queued.Media.Id))
				.Union(ctx.FeedRunItems.IgnoreQueryFilters().Select(item => item.MediaId));
		}

		public async Task<int> TrimDetailsAsync(IQueryable<MediaDetailsRow> details, DateTimeOffset now, CancellationToken cancellationToken)
		{
			var titles = details.Select(row => row.MediaId);
			await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
			await ctx
				.Media.Where(media => titles.Contains(media.Id))
				.ExecuteUpdateAsync(setters => setters.SetProperty(media => media.DetailsTrimmedAt, (DateTimeOffset?)now.ToUniversalTime()), cancellationToken);
			var removed = await details.ExecuteDeleteAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return removed;
		}
	}
}

file sealed record KeyOwner(string Source, string MediaType, string Id, Guid MediaId);
