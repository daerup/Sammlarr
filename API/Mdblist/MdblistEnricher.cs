using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistEnricher(MdblistTitles titles) : IDetailsEnricher
{
	public MediaFields Fills { get; } = MediaFields.Rated(RemoteService.Mdblist.SuppliedRatingSources());

	public MediaFields Defers => MediaFields.None;

	public IReadOnlyList<MediaProvider> Accepts => MdblistKey.Order;

	public async Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		var answer = await titles.FindAsync([id], cancellationToken);
		return answer.Map(title => MdblistFound.Of(title, id.Type));
	}
}
