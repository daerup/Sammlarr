namespace E2E.Tests.PlexFree;

[PlexFree]
public class PlexFreeStackTests
{
	private const int ProjectHailMary = 687163;

	[Test]
	[Repeat(2)]
	public async Task Radarr_WhenProjectHailMaryIsAdded_HoldsOnlyThatMovie()
	{
		var radarr = (await PlexFreeApp.StackAsync()).Radarr;
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

	[Test]
	[Repeat(2)]
	public async Task Web_OnAFreshStack_SendsAPageToSetup()
	{
		var stack = await PlexFreeApp.StackAsync();
		await stack.SetupCodeAsync();
		using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(stack.BaseUrl) };
		using var request = new HttpRequestMessage(HttpMethod.Get, "/");
		request.Headers.Accept.ParseAdd("text/html");

		using var response = await http.SendAsync(request);

		await Assert.That(response.Headers.Location?.OriginalString).IsEqualTo("/setup");
	}
}
