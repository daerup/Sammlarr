namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SelfHostedStackTests
{
	[Test]
	[Repeat(2)]
	public async Task Radarr_WhenProjectHailMaryIsAdded_HoldsOnlyThatMovie()
	{
		var radarr = (await SelfHostedApp.StackAsync()).Radarr;

		await radarr.AddMovieAsync(Titles.ProjectHailMaryTmdb);

		var titles = (await radarr.GetAsync("movie")).AsArray().Select(added => added!["title"]!.GetValue<string>());
		await Assert.That(titles).IsEquivalentTo([Titles.ProjectHailMary]);
	}
}
