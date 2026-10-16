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
	public Task PrimeAsync(IReadOnlyList<PrimedTitle> titles, DetailsRun run, IReadOnlySet<RemoteService> idServices, CancellationToken cancellationToken)
	{
		return Task.WhenAll(
			primers.Select(primer =>
				PrimeAsync(
					primer,
					details.Enriches(primer.Remote) ? Enriched(primer.Remote, titles, run) : Converted(primer.Remote, titles, idServices),
					cancellationToken
				)
			)
		);
	}

	private static async Task PrimeAsync(IBurstPrimer primer, List<IReadOnlyList<IExternalMediaId>> wanted, CancellationToken cancellationToken)
	{
		if (wanted.Count > 0)
		{
			await primer.PrimeAsync(wanted, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}
	}

	private List<IReadOnlyList<IExternalMediaId>> Enriched(RemoteService remote, IReadOnlyList<PrimedTitle> titles, DetailsRun run)
	{
		if (!run.Connected.Contains(remote))
		{
			return [];
		}

		var load = capacity.Of(remote);
		var takes = load.Takes(clock.Now, load.EnrichWait);
		var inTime = takes && load.AnswersWithin(clock.Now, options.ShownWait);
		return [.. titles.Where(title => Wants(remote, title, run.Needs, (takes, inTime))).Select(title => title.Ids)];
	}

	private static List<IReadOnlyList<IExternalMediaId>> Converted(
		RemoteService remote,
		IReadOnlyList<PrimedTitle> titles,
		IReadOnlySet<RemoteService> idServices
	)
	{
		return idServices.Contains(remote) ? [.. titles.Where(title => title.Stored.IsNone()).Select(title => title.Ids)] : [];
	}

	private bool Wants(RemoteService remote, PrimedTitle title, FieldNeeds needs, (bool Takes, bool InTime) lane)
	{
		var asked = details.FillsFor(remote, needs, title.Stored);
		return (lane.Takes && !asked.Required.IsEmpty) || (lane.InTime && !asked.Shown.IsEmpty);
	}
}

internal readonly record struct PrimedTitle(IReadOnlyList<IExternalMediaId> Ids, Optional<CachedDetails> Stored);
