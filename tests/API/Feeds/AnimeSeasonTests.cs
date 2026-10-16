using API.Feeds;

namespace API.Tests.Feeds;

public class AnimeSeasonTests
{
	[Test]
	[Arguments(1, "WINTER")]
	[Arguments(3, "WINTER")]
	[Arguments(4, "SPRING")]
	[Arguments(6, "SPRING")]
	[Arguments(7, "SUMMER")]
	[Arguments(9, "SUMMER")]
	[Arguments(10, "FALL")]
	[Arguments(12, "FALL")]
	public async Task Of_EachMonth_FallsInItsQuarter(int month, string season)
	{
		await Assert.That(AnimeSeason.Of(new DateOnly(2026, month, 6))).IsEqualTo(new AnimeSeason(season, 2026));
	}

	[Test]
	public async Task Next_AcrossAYearsEnd_IsWinterOfTheNextYear()
	{
		await Assert.That(AnimeSeason.Of(new DateOnly(2026, 11, 6)).Next()).IsEqualTo(new AnimeSeason("WINTER", 2027));
	}

	[Test]
	public async Task Next_WithinAYear_IsTheFollowingQuarter()
	{
		await Assert.That(AnimeSeason.Of(new DateOnly(2026, 5, 1)).Next()).IsEqualTo(new AnimeSeason("SUMMER", 2026));
	}
}
