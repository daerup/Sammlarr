using Contracts.Blacklist;
using Contracts.Media;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Blacklist;

[Binding]
public class BlacklistSteps(MediaDetailContext detailCtx, SammlarrScenarioContext sammlarr, CachedMediaContext cachedMedia)
{
	[Given("{string} is on the blacklist")]
	public async Task GivenOnBlacklist(string key) => await sammlarr.Resolve<IBlacklistRepository>().AddAsync(cachedMedia.MediaIds[key]);

	[When("I add {string} to the blacklist")]
	public async Task WhenAdd(string key) => await sammlarr.Resolve<IBlacklistService>().AddToBacklistAsync(cachedMedia.MediaIds[key]);

	[When("I remove {string} from the blacklist")]
	public async Task WhenRemove(string key) => await sammlarr.Resolve<IBlacklistService>().RemoveFromBacklistAsync(cachedMedia.MediaIds[key]);

	[When("I get the blacklist IDs")]
	public async Task WhenGetBlacklistIds() => detailCtx.Ids = await sammlarr.Resolve<IBlacklistService>().GetBlacklistIdsAsync();

	[When("I get the blacklist items")]
	public async Task WhenGetBlacklistItems() =>
		detailCtx.Items = await sammlarr.Resolve<IBlacklistService>().GetBlacklistItems().Select(i => i.Detail).ToListAsync();

	[Then("the blacklist contains {int} entry")]
	public async Task ThenBlacklistContains(int count)
	{
		var entries = await sammlarr.Resolve<IBlacklistRepository>().GetBlacklistedEntriesAsync();
		await Assert.That(entries).Count().IsEqualTo(count);
	}

	[Then("the blacklist is empty")]
	public async Task ThenBlacklistIsEmpty()
	{
		var entries = await sammlarr.Resolve<IBlacklistRepository>().GetBlacklistedEntriesAsync();
		await Assert.That(entries).IsEmpty();
	}
}
