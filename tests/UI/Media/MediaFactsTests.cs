using Domain.Media;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaFactsTests
{
	[Test]
	[Arguments(148.4, "2 h 28 min")]
	[Arguments(120.0, "2 h")]
	[Arguments(44.5, "45 min")]
	public async Task GetDisplayFacts_OfAMovie_ReadsItsRuntimeToTheMinute(double minutes, string expected)
	{
		var facts = MediaFacts.GetDisplayFacts(new MovieDetails { Runtime = TimeSpan.FromMinutes(minutes) });

		await Assert.That(facts).IsEquivalentTo([expected]);
	}

	[Test]
	public async Task GetDisplayFacts_OfAMovieUnderHalfAMinute_ShowsNoRuntime()
	{
		var facts = MediaFacts.GetDisplayFacts(new MovieDetails { Runtime = TimeSpan.FromSeconds(20) });

		await Assert.That(facts).IsEmpty();
	}

	[Test]
	public async Task GetDisplayFacts_OfAShow_CountsSeasonsAndEpisodes()
	{
		var facts = MediaFacts.GetDisplayFacts(new ShowDetails { Seasons = 1, Episodes = 22 });

		await Assert.That(facts).IsEquivalentTo(["1 season", "22 episodes"]);
	}
}
