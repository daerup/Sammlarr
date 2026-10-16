using Contracts.Housekeeping;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.Logging;
using Observability.Usage;

namespace Core.Media;

internal sealed class DuplicateTitles(
	IIdentityConflicts conflicts,
	IMediaRepository media,
	IIdentityConfirmation confirmation,
	IMediaMerge merge,
	ILogger<DuplicateTitles> logger
) : IHousekeepingStep
{
	public HousekeepingStep Step => HousekeepingStep.DuplicateTitles;

	public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(UsageCause.Housekeeping);
		var due = await conflicts.DueAsync(now, cancellationToken);
		var stored = await media.GetAsync([.. due.SelectMany(note => new[] { note.Held, note.Other }).Distinct()], cancellationToken);
		var keys = stored.ToDictionary(title => title.Identity.Id, title => title.Identity.Keys);
		List<(IdentityConflict Note, Optional<DistinctReason> Reason)> judged =
		[
			.. due.Where(note => keys.ContainsKey(note.Held) && keys.ContainsKey(note.Other))
				.Select(note => (note, IdentityConflict.Contradicts(keys[note.Held], keys[note.Other]))),
		];
		foreach (var (note, reason) in judged)
		{
			await reason.Apply(contradiction => conflicts.SaveAsync(note with { State = new Distinct(contradiction, now) }, cancellationToken));
		}

		var replies = await confirmation.AskAsync(
			[.. judged.Where(each => each.Reason.IsNone()).Select(each => new ConfirmationRequest(each.Note, keys[each.Note.Held]))],
			cancellationToken
		);
		var merged = 0;
		foreach (var reply in replies)
		{
			merged += await SettleAsync(reply, now, cancellationToken);
		}

		return merged;
	}

	private async Task<int> SettleAsync(Confirmation reply, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var note = reply.Note;
		return reply.Answer switch
		{
			Found<IIdentityKey> found when found.Value.Equals(note.Claimed) => await MergeAsync(note, reply.By, now, cancellationToken),
			Found<IIdentityKey> found => await SavedAsync(note with { State = new Distinct(new AnsweredElsewhere(reply.By, found.Value), now) }, cancellationToken),
			NoMatch => await SavedAsync(note.Asked(new LastAsk(reply.By, AskOutcome.NoMatch, now)), cancellationToken),
			Unavailable { Sent: false } => 0,
			Unavailable => await SavedAsync(note.Asked(new LastAsk(reply.By, AskOutcome.Unavailable, now)), cancellationToken),
			Faulted => await SavedAsync(note.Asked(new LastAsk(reply.By, AskOutcome.Faulted, now)), cancellationToken),
		};
	}

	private async Task<int> MergeAsync(IdentityConflict note, RemoteService by, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var outcome = await merge.MergeAsync(note.Held, note.Other, cancellationToken);
		return outcome switch
		{
			Merged merged => Logged(merged, note, by),
			Contradicted contradicted => await SavedAsync(note with { State = new Distinct(contradicted.Reason, now) }, cancellationToken),
			NotStored => 0,
		};
	}

	private int Logged(Merged merged, IdentityConflict note, RemoteService by)
	{
		logger.Merged(merged, note, $"{by}");
		return 1;
	}

	private async Task<int> SavedAsync(IdentityConflict note, CancellationToken cancellationToken)
	{
		await conflicts.SaveAsync(note, cancellationToken);
		return 0;
	}
}
