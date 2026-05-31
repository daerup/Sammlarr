using Contracts.Blacklist;
using Contracts.Media;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Reqnroll.Assist;

namespace Integration.Tests.Blacklist;

[Binding]
public class BlacklistSteps(BlacklistScenarioContext ctx, SammlarrScenarioContext sammlarr, LocalMediaContext cachedMedia)
{
	[Given("the following are blacklisted:")]
	public async Task GivenBlacklisted(DataTable table)
	{
		var repository = sammlarr.Resolve<IBlacklistRepository>();
		foreach (var row in table.CreateSet<MediaRow>())
			await repository.AddAsync(cachedMedia.MediaIds[row.Media!]);
	}

	[When("I add {string} to the blacklist")]
	public async Task WhenAdd(string key) => await sammlarr.Resolve<IBlacklistService>().AddToBacklistAsync(cachedMedia.MediaIds[key]);

	[When("I remove {string} from the blacklist")]
	public async Task WhenRemove(string key) => await sammlarr.Resolve<IBlacklistService>().RemoveFromBacklistAsync(cachedMedia.MediaIds[key]);

	[When("I get the blacklist IDs")]
	public async Task WhenGetBlacklistIds() => ctx.Ids = await sammlarr.Resolve<IBlacklistService>().GetBlacklistIdsAsync();

	[When("I get the blacklist items")]
	public async Task WhenGetBlacklistItems() =>
		ctx.Items = await sammlarr.Resolve<IBlacklistService>().GetBlacklistItems().Select(i => i.Detail).ToListAsync();

	[Then("the blacklist items are:")]
	public Task ThenItemsAre(DataTable table) => MediaDetailAssertions.AssertItemsAsync(ctx.Items!, table);

	[Then("the blacklist IDs include:")]
	public Task ThenIdsInclude(DataTable table) => MediaDetailAssertions.AssertIdsIncludeAsync(ctx.Ids!, table, cachedMedia);

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
