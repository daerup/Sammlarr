using Contracts;
using Contracts.Media;
using Contracts.Providers;
using Domain.Media;
using Domain.Providers;
using Shared.Options;

namespace Core.Media;

internal sealed class BurstPrimers(
	IEnumerable<IBurstPrimer> primers,
	DetailsFetcher details,
	IRemoteCapacity capacity,
	IDateTimeProvider clock,
	FeedLoadOptions options
)
{
	public async Task PrimeAsync(IReadOnlyList<PrimedTitle> titles, DetailsRun run, CancellationToken cancellationToken)
	{
		foreach (var primer in primers.Where(primer => run.Connected.Contains(primer.Remote)))
		{
			var load = capacity.Of(primer.Remote);
			var takes = load.Takes(clock.Now, load.EnrichWait);
			var inTime = takes && load.AnswersWithin(clock.Now, options.ShownWait);
			List<IReadOnlyList<IExternalMediaId>> wanted =
			[
				.. titles.Where(title => Wants(primer.Remote, title, run.Needs, (takes, inTime))).Select(title => title.Ids),
			];
			if (wanted.Count > 0)
			{
				await primer.PrimeAsync(wanted, cancellationToken);
			}
		}
	}

	private bool Wants(RemoteService remote, PrimedTitle title, FieldNeeds needs, (bool Takes, bool InTime) lane)
	{
		var asked = details.FillsFor(remote, needs, title.Stored);
		return (lane.Takes && !asked.Required.IsEmpty) || (lane.InTime && !asked.Shown.IsEmpty);
	}
}

internal readonly record struct PrimedTitle(IReadOnlyList<IExternalMediaId> Ids, Optional<CachedDetails> Stored);
