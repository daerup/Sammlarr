using Contracts;
using Contracts.Identity;
using Contracts.Media;
using Core.Jobs;
using Domain.Media;
using Microsoft.Extensions.Logging;

namespace Core.Media;

internal sealed class ConflictReview(
	IIdentityConflicts conflicts,
	IMediaRepository media,
	IMediaMerge merge,
	JobTurn turn,
	TaskActivity activity,
	ICurrentUser currentUser,
	IDateTimeProvider clock,
	ILogger<ConflictReview> logger
) : IConflictReview
{
	public bool WaitsForAJob => turn.Taken;

	public async Task<IReadOnlyList<StoredTwice>> PairsAsync(CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		var notes = await conflicts.AllAsync(cancellationToken);
		List<MediaId> titles = [.. notes.SelectMany(note => new[] { note.Held, note.Other }).Distinct()];
		var stored = (await media.GetAsync(titles, cancellationToken)).ToDictionary(title => title.Identity.Id);
		var uses = await conflicts.UsesAsync(titles, cancellationToken);
		return
		[
			.. notes
				.Where(note => stored.ContainsKey(note.Held) && stored.ContainsKey(note.Other))
				.Select(note => new StoredTwice(note, new StoredTitle(stored[note.Held], uses[note.Held]), new StoredTitle(stored[note.Other], uses[note.Other]))),
		];
	}

	public async Task<IReadOnlyList<Refused>> ApplyAsync(IReadOnlyList<ReviewChoice> choices, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		List<Refused> refused = [];
		using (await turn.TakeAsync(cancellationToken))
		{
			foreach (var choice in choices)
			{
				(await SettleAsync(choice, cancellationToken)).Apply(refused.Add);
			}
		}

		activity.Progressed();
		return refused;
	}

	private async Task<Optional<Refused>> SettleAsync(ReviewChoice choice, CancellationToken cancellationToken)
	{
		return choice.Choice switch
		{
			ConflictChoice.Merge => await MergeAsync(choice.Note, cancellationToken),
			ConflictChoice.KeepApart => await KeptApartAsync(choice.Note, cancellationToken),
			ConflictChoice.Leave => new None(),
		};
	}

	private async Task<Optional<Refused>> MergeAsync(IdentityConflict note, CancellationToken cancellationToken)
	{
		var outcome = await merge.MergeAsync(note.Held, note.Other, cancellationToken);
		return outcome switch
		{
			Merged merged => Logged(merged, note),
			Contradicted contradicted => await RefusedAsync(note, contradicted.Reason, cancellationToken),
			NotStored => new None(),
		};
	}

	private Optional<Refused> Logged(Merged merged, IdentityConflict note)
	{
		logger.Merged(merged, note, $"hand ({currentUser.Id})");
		return new None();
	}

	private async Task<Optional<Refused>> RefusedAsync(IdentityConflict note, DistinctReason reason, CancellationToken cancellationToken)
	{
		await conflicts.SaveAsync(note with { State = new Distinct(reason, clock.Now) }, cancellationToken);
		return Some.Of(new Refused(note, reason));
	}

	private async Task<Optional<Refused>> KeptApartAsync(IdentityConflict note, CancellationToken cancellationToken)
	{
		await conflicts.SaveAsync(note with { State = new Distinct(new KeptApartByHand(currentUser.Id), clock.Now) }, cancellationToken);
		return new None();
	}
}
