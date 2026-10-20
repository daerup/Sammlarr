using System.Collections.Concurrent;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class IdentityClaims(IDbContextFactory<SammlarrDbContext> contexts, MediaWriteLock writeLock, TimeProvider clock) : IIdentityClaims, IDisposable
{
	private const int ConstraintFailed = 19;

	private readonly ConcurrentQueue<PendingClaim> _queue = new();
	private readonly SemaphoreSlim _committing = new(1, 1);

	public void Dispose() => _committing.Dispose();

	public async Task<ClaimOutcome> ClaimAsync(IdentityClaim claim, CancellationToken cancellationToken = default)
	{
		var pending = new PendingClaim(claim, new TaskCompletionSource<ClaimOutcome>(TaskCreationOptions.RunContinuationsAsynchronously));
		_queue.Enqueue(pending);
		await CommitQueuedAsync();
		return await pending.Outcome.Task.WaitAsync(cancellationToken);
	}

	private async Task CommitQueuedAsync()
	{
		await _committing.WaitAsync();
		try
		{
			List<PendingClaim> group = [];
			while (_queue.TryDequeue(out var queued))
			{
				group.Add(queued);
			}

			await SettleAsync(group);
		}
		finally
		{
			_committing.Release();
		}
	}

	private async Task SettleAsync(IReadOnlyList<PendingClaim> group)
	{
		if (group.Count == 0)
		{
			return;
		}

		IReadOnlyList<ClaimOutcome> outcomes;
		try
		{
			outcomes = await CommitAsync([.. group.Select(pending => pending.Claim)]);
		}
		catch (Exception exception)
		{
			foreach (var pending in group)
			{
				pending.Outcome.SetException(exception);
			}

			return;
		}

		foreach (var (pending, outcome) in group.Zip(outcomes))
		{
			pending.Outcome.SetResult(outcome);
		}
	}

	private async Task<IReadOnlyList<ClaimOutcome>> CommitAsync(IReadOnlyList<IdentityClaim> claims)
	{
		using var _ = await writeLock.AcquireAsync(CancellationToken.None);
		await using var ctx = await contexts.CreateDbContextAsync();
		await using var transaction = await ctx.Database.BeginTransactionAsync();
		var library = await LibraryAsync(ctx, claims);
		List<ClaimOutcome> outcomes = [];
		foreach (var claim in claims)
		{
			var decided = ClaimRule.Decide(claim, library.Owners, library.Held, MediaId.New());
			outcomes.Add(await WriteAsync(ctx, library, claim, decided));
			foreach (var note in Notes(library, claim, decided))
			{
				await ctx.NoteConflictAsync(note, CancellationToken.None);
			}
		}

		await transaction.CommitAsync();
		return outcomes;
	}

	private IEnumerable<IdentityConflict> Notes(Library library, IdentityClaim claim, ClaimOutcome decided)
	{
		return decided switch
		{
			Claimed claimed => claimed.Disputed.Select(disputed => Note(claimed.Id, disputed.Owner, disputed.Key, claim.Sources)),
			Conflicted conflicted => conflicted.Owners.Skip(1).Select(other => Note(conflicted.Owners.First(), other, OwnedBy(library, claim, other), claim.Sources)),
		};
	}

	private IdentityConflict Note(MediaId held, MediaId other, IIdentityKey claimed, ValueSet<RemoteService> sources)
	{
		return new IdentityConflict(held, other, claimed, sources, clock.GetUtcNow(), 0, clock.GetUtcNow(), new None(), new Undecided());
	}

	private static IIdentityKey OwnedBy(Library library, IdentityClaim claim, MediaId owner)
	{
		return claim.Keys.First(key => library.Owners.GetValueOrNone(key).Match(found => found == owner, () => false));
	}

	private async Task<ClaimOutcome> WriteAsync(SammlarrDbContext ctx, Library library, IdentityClaim claim, ClaimOutcome decided)
	{
		if (decided is not Claimed claimed)
		{
			return decided;
		}

		var held = library.Held.GetValueOrNone(claimed.Id);
		List<IIdentityKey> added = [.. claimed.Keys.Where(key => !held.GetValueOr([]).Contains(key))];
		var row = held.Match(
			_ => ctx.Media.Attach(new MediaRow { Id = claimed.Id.Value }).Entity,
			() => ctx.Media.Add(new MediaRow { Id = claimed.Id.Value }).Entity
		);
		ctx.ExternalMediaIds.AddRange(added.Select(key => key.ToExternalIdEntity(row)));
		try
		{
			await ctx.SaveChangesAsync();
			library.Record(claimed);
			return claimed;
		}
		catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: ConstraintFailed })
		{
			ctx.ChangeTracker.Clear();
			var owners = await ctx.KeyOwnersAsync(added, CancellationToken.None);
			await held.Apply(_ => NoteRaceAsync(ctx, claimed.Id, owners, claim.Sources));
			return new Conflicted([.. owners.Values], [.. added]);
		}
		finally
		{
			ctx.ChangeTracker.Clear();
		}
	}

	private async Task NoteRaceAsync(SammlarrDbContext ctx, MediaId held, IReadOnlyDictionary<IIdentityKey, MediaId> owners, ValueSet<RemoteService> sources)
	{
		foreach (var owner in owners.Where(entry => entry.Value != held).GroupBy(entry => entry.Value))
		{
			await ctx.NoteConflictAsync(Note(held, owner.Key, owner.First().Key, sources), CancellationToken.None);
		}
	}

	private static async Task<Library> LibraryAsync(SammlarrDbContext ctx, IReadOnlyList<IdentityClaim> claims)
	{
		List<IIdentityKey> keys = [.. claims.SelectMany(claim => claim.Keys).Distinct()];
		List<Guid> pinned = [.. claims.Select(claim => claim.Pinned).WhereSome().Select(id => id.Value)];
		var owning = await ctx.OwnersAsync(keys, CancellationToken.None);
		var rows = await ctx
			.Media.AsNoTracking()
			.Where(media => EF.Parameter(pinned).Contains(media.Id) || EF.Parameter(owning).Contains(media.Id))
			.Include(media => media.ExternalIds)
			.ToListAsync();
		return new Library(
			rows.ToDictionary(
				row => MediaId.From(row.Id),
				row => (ValueSet<IIdentityKey>)[.. row.ExternalIds.Select(external => (IIdentityKey)external.ToExternalMediaId())]
			)
		);
	}

	private sealed record PendingClaim(IdentityClaim Claim, TaskCompletionSource<ClaimOutcome> Outcome);

	private sealed class Library(Dictionary<MediaId, ValueSet<IIdentityKey>> held)
	{
		public Dictionary<MediaId, ValueSet<IIdentityKey>> Held { get; } = held;

		public Dictionary<IIdentityKey, MediaId> Owners { get; } =
			held.SelectMany(row => row.Value.Select(key => (Key: key, Row: row.Key))).ToDictionary(entry => entry.Key, entry => entry.Row);

		public void Record(Claimed claimed)
		{
			Held[claimed.Id] = claimed.Keys;
			foreach (var key in claimed.Keys)
			{
				Owners[key] = claimed.Id;
			}
		}
	}
}
