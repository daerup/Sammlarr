namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SelfHostedStackTests
{
	private const int ProjectHailMary = 687163;

	[Test]
	[Repeat(2)]
	public async Task Radarr_WhenProjectHailMaryIsAdded_HoldsOnlyThatMovie()
	{
		var radarr = (await SelfHostedApp.StackAsync()).Radarr;
		var movie = (await radarr.GetAsync($"movie/lookup?term=tmdb:{ProjectHailMary}"))[0]!;
		var profile = (await radarr.GetAsync("qualityprofile")).AsArray().First(candidate => candidate!["name"]!.GetValue<string>() == "HD-1080p")!;

		await radarr.PostAsync(
			"movie",
			new
			{
				title = movie["title"]!.GetValue<string>(),
				tmdbId = ProjectHailMary,
				year = movie["year"]!.GetValue<int>(),
				qualityProfileId = profile["id"]!.GetValue<int>(),
				rootFolderPath = "/movies",
				monitored = true,
				minimumAvailability = "announced",
				addOptions = new { searchForMovie = false },
			}
		);

		var titles = (await radarr.GetAsync("movie")).AsArray().Select(added => added!["title"]!.GetValue<string>());
		await Assert.That(titles).IsEquivalentTo(["Project Hail Mary"]);
	}
}
