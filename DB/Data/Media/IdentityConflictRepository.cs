using Contracts.Media;
using Domain.Identity;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class IdentityConflictRepository(IDbContextFactory<SammlarrDbContext> contexts) : IIdentityConflicts
{
	public async Task<IReadOnlyList<IdentityConflict>> DueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		var due = now.ToUniversalTime();
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.IdentityConflicts.AsNoTracking()
			.Include(row => row.Sources)
			.Where(row => row.Distinct == null && row.AskAfter <= due)
			.ToListAsync(cancellationToken);
		return [.. rows.Select(Conflict)];
	}

	public async Task<IReadOnlyList<IdentityConflict>> AllAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.IdentityConflicts.AsNoTracking().Include(row => row.Sources).ToListAsync(cancellationToken);
		return [.. rows.Select(Conflict)];
	}

	public async Task SaveAsync(IdentityConflict conflict, CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var row = await ctx.IdentityConflicts.SingleOrDefaultAsync(
			stored => stored.HeldId == conflict.Held.Value && stored.OtherId == conflict.Other.Value,
			cancellationToken
		);
		Optional.Of(row).Apply(stored => Settle(stored, conflict));
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static IdentityConflict Conflict(IdentityConflictRow row)
	{
		return new IdentityConflict(
			MediaId.From(row.HeldId),
			MediaId.From(row.OtherId),
			MediaIdConversionExtensions.Key(row.ClaimedSource, row.ClaimedType, row.ClaimedId),
			[.. row.Sources.Select(source => source.Service)],
			row.SeenAt,
			row.Attempts,
			row.AskAfter,
			Optional.Of(row.AskedAt).Map(at => new LastAsk(row.AskedService!.Value, row.AskedOutcome!.Value, at)),
			State(row)
		);
	}

	private static void Settle(IdentityConflictRow row, IdentityConflict conflict)
	{
		row.Attempts = conflict.Attempts;
		row.AskAfter = conflict.AskAfter.ToUniversalTime();
		row.AskedService = conflict.LastAsk.Map(ask => ask.Service).ToNullable();
		row.AskedOutcome = conflict.LastAsk.Map(ask => ask.Outcome).ToNullable();
		row.AskedAt = conflict.LastAsk.Map(ask => ask.At.ToUniversalTime()).ToNullable();
		row.Distinct = null;
		row.DistinctAt = null;
		row.ContradictedBy = null;
		row.AnsweredBy = null;
		row.AnswerSource = null;
		row.AnswerType = null;
		row.AnswerId = null;
		row.KeptApartBy = null;
		if (conflict.State is Distinct distinct)
		{
			Settle(row, distinct);
		}
	}

	private static void Settle(IdentityConflictRow row, Distinct distinct)
	{
		row.DistinctAt = distinct.At.ToUniversalTime();
		switch (distinct.Reason)
		{
			case Contradiction contradiction:
				row.Distinct = DistinctKind.Contradiction;
				row.ContradictedBy = contradiction.Source;
				break;
			case TypesDiffer:
				row.Distinct = DistinctKind.TypesDiffer;
				break;
			case AnsweredElsewhere answered:
				row.Distinct = DistinctKind.AnsweredElsewhere;
				row.AnsweredBy = answered.By;
				row.AnswerSource = answered.Answer.Source;
				row.AnswerType = answered.Answer.Type;
				row.AnswerId = answered.Answer.Value;
				break;
			case KeptApartByHand kept:
				row.Distinct = DistinctKind.KeptApartByHand;
				row.KeptApartBy = kept.By.Value;
				break;
		}
	}

	private static ConflictState State(IdentityConflictRow row)
	{
		return Optional.Of(row.Distinct).Map(kind => (ConflictState)new Distinct(Reason(row, kind), row.DistinctAt!.Value)).GetValueOr(new Undecided());
	}

	private static DistinctReason Reason(IdentityConflictRow row, DistinctKind kind)
	{
		return kind switch
		{
			DistinctKind.Contradiction => new Contradiction(row.ContradictedBy!.Value),
			DistinctKind.TypesDiffer => new TypesDiffer(),
			DistinctKind.AnsweredElsewhere => new AnsweredElsewhere(
				row.AnsweredBy!.Value,
				MediaIdConversionExtensions.Key(row.AnswerSource!.Value, row.AnswerType!.Value, row.AnswerId!)
			),
			DistinctKind.KeptApartByHand => new KeptApartByHand(UserId.From(row.KeptApartBy!.Value)),
		};
	}
}
