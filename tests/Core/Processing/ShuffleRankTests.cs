using Core.Processing;

namespace Core.Tests.Processing;

public class ShuffleRankTests
{
	private static readonly Guid Key = new("0f8fad5b-d9cb-469f-a165-70867728950e");

	[Test]
	[Arguments("7c9e6679-7425-40de-944b-e07fc1f90ae7", 17194703832663560596UL)]
	[Arguments("e4eaaaf2-d142-11e1-b3e4-080027620cdd", 907737891233472529UL)]
	public async Task Of_ForAFixedSeedAndKey_ReturnsTheSameRankEveryTime(string seed, ulong rank)
	{
		await Assert.That(ShuffleRank.Of(Guid.Parse(seed), Key)).IsEqualTo(rank);
	}

	[Test]
	public async Task Reseed_ForTheSameRun_GivesTheSameSeedAndAnotherRunAnother()
	{
		var run = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(2));

		var seed = ShuffleRank.Reseed(Key, run);

		await Assert.That(ShuffleRank.Reseed(Key, run.ToUniversalTime())).IsEqualTo(seed);
		await Assert.That(ShuffleRank.Reseed(Key, run.AddTicks(1))).IsNotEqualTo(seed);
		await Assert.That(seed).IsNotEqualTo(Key);
	}
}
