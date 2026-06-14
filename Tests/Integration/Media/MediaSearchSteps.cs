using Contracts.Media;
using Contracts.Media.Search;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Media;

[Binding]
public class MediaSearchSteps(MediaSearchScenarioContext ctx, SammlarrScenarioContext sammlarr)
{
	[When("I search for {string} with types {string}")]
	public async Task WhenSearch(string query, string types)
	{
		var mediaTypes = types
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(type => Enum.Parse<MediaType>(type, ignoreCase: true))
			.ToList();
		ctx.Results = await sammlarr.Resolve<IMediaSearchService>().SearchAsync(query, mediaTypes);
	}

	[Then("the search results are:")]
	public Task ThenResultsAre(DataTable table) => MediaDetailAssertions.AssertItemsAsync(ctx.Results!, table);

	[Then("the search results are empty")]
	public async Task ThenResultsEmpty() => await Assert.That(ctx.Results!).IsEmpty();

	[Then("the search result {string} has the id {string}")]
	public async Task ThenResultHasId(string title, string id)
	{
		var (provider, value) = MediaProviders.ParseToken(id);
		var result = ctx.Results!.Single(item => item.Title == title);
		await Assert.That(result.Identity.Externals.Any(external => external.Source == provider && external.Value == value)).IsTrue();
	}
}
