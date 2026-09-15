using System.Net;
using API.Plex;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Shared;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexIdConverterResilienceTests
{
	private static readonly Optional<ResolutionHints> Hints = Some.Of(
		new ResolutionHints
		{
			Title = "match",
			Year = 2021,
			MediaType = MediaType.Movie,
		}
	);

	[Test]
	public async Task ConvertAsync_WhenOneCandidateLookupIsRateLimited_StillResolvesFromAnotherCandidate()
	{
		var throttled = PlexCandidate.Create();
		var wanted = PlexCandidate.Create();
		var stub = new PlexStub().Returns(throttled).Returns(wanted).RateLimits(throttled);
		var testee = new PlexIdConverter(null!, stub.CreateApi());

		var result = await testee.ConvertAsync([wanted.TmdbId], Hints);

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(wanted.PlexId));
	}

	[Test]
	public async Task ConvertAsync_WhenOneCandidateLookupFailsPermanently_StillResolvesFromAnotherCandidate()
	{
		var broken = PlexCandidate.Create();
		var wanted = PlexCandidate.Create();
		var stub = new PlexStub().Returns(broken).Returns(wanted).Fails(broken, HttpStatusCode.NotFound);
		var testee = new PlexIdConverter(null!, stub.CreateApi());

		var result = await testee.ConvertAsync([wanted.TmdbId], Hints);

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(wanted.PlexId));
	}

	[Test]
	public async Task ConvertAsync_WhenEveryCandidateIsRateLimited_ThrowsProviderUnavailable()
	{
		var first = PlexCandidate.Create();
		var second = PlexCandidate.Create();
		var stub = new PlexStub().Returns(first).Returns(second).RateLimits(first).RateLimits(second);
		var testee = new PlexIdConverter(PlexStub.UnmatchedTmdbApi(), stub.CreateApi());

		await Assert.That(async () => await testee.ConvertAsync([PlexCandidate.Create().TmdbId], Hints)).Throws<ProviderUnavailableException>();
	}

	[Test]
	public async Task ConvertAsync_WhenTheExactTitleRanksBelowTheLookupCap_StillResolvesIt()
	{
		var wanted = PlexCandidate.Create("Up", 2009);
		var stub = new PlexStub();
		foreach (var index in Enumerable.Range(0, 8))
		{
			stub.Returns(PlexCandidate.Create($"Fed Up {index}", 1990));
		}
		stub.Returns(wanted);
		var testee = new PlexIdConverter(PlexStub.UnmatchedTmdbApi(), stub.CreateApi());

		var result = await testee.ConvertAsync(
			[wanted.TmdbId],
			Some.Of(
				new ResolutionHints
				{
					Title = "Up",
					Year = 2009,
					MediaType = MediaType.Movie,
				}
			)
		);

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(wanted.PlexId));
		await Assert.That(stub.MetadataLookups()).IsLessThanOrEqualTo(5);
	}

	[Test]
	public async Task ConvertAsync_WhenTwoResultsShareATitle_PrefersTheYearFromTheHints()
	{
		var remake = PlexCandidate.Create("Ben-Hur", 2016);
		var wanted = PlexCandidate.Create("Ben-Hur", 1959);
		var stub = new PlexStub().Returns(remake).Returns(wanted);
		var testee = new PlexIdConverter(PlexStub.UnmatchedTmdbApi(), stub.CreateApi());

		var result = await testee.ConvertAsync(
			[wanted.TmdbId],
			Some.Of(
				new ResolutionHints
				{
					Title = "Ben-Hur",
					Year = 1959,
					MediaType = MediaType.Movie,
				}
			)
		);

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(wanted.PlexId));
		await Assert.That(stub.MetadataLookups(wanted.PlexId.Value)).IsEqualTo(1);
	}

	[Test]
	public async Task ConvertAsync_WhenTheProviderQualifiesTheTitleWithARegion_StillPrefersTheYearMatch()
	{
		var wanted = PlexCandidate.Create("The Office (US)", 2005);
		var stub = new PlexStub();
		foreach (var year in new[] { 2001, 1966, 2019, 2010 })
		{
			stub.Returns(PlexCandidate.Create("The Office", year));
		}
		stub.Returns(wanted);
		var testee = new PlexIdConverter(PlexStub.UnmatchedTmdbApi(), stub.CreateApi());

		var result = await testee.ConvertAsync(
			[wanted.TmdbId],
			Some.Of(
				new ResolutionHints
				{
					Title = "The Office",
					Year = 2005,
					MediaType = MediaType.Movie,
				}
			)
		);

		await Assert.That(result).IsSuccess().And.Multiple(async externals => await Assert.That(externals).Contains(wanted.PlexId));
	}

	[Test]
	public async Task ConvertAsync_WhenNoCandidateMatches_LooksUpAtMostThreeCandidates()
	{
		var stub = new PlexStub();
		for (var i = 0; i < 5; i++)
		{
			stub.Returns(PlexCandidate.Create());
		}
		var testee = new PlexIdConverter(PlexStub.UnmatchedTmdbApi(), stub.CreateApi());

		var result = await testee.ConvertAsync([PlexCandidate.Create().TmdbId], Hints);

		await Assert.That(result).IsError();
		await Assert.That(stub.MetadataLookups()).IsLessThanOrEqualTo(3);
	}

	[Test]
	public async Task ConvertAsync_WhenTwoConcurrentResolvesShareARatingKey_LooksUpThatRatingKeyOnce()
	{
		var shared = PlexCandidate.Create();
		var stub = new PlexStub { MetadataGate = new TaskCompletionSource() };
		stub.Returns(shared);
		var plexApi = stub.CreateApi();
		var testee = new PlexIdConverter(null!, plexApi);

		var first = testee.ConvertAsync([shared.TmdbId], Hints);
		await WaitForMetadataLookups(stub, 1);
		var second = testee.ConvertAsync([shared.TmdbId], Hints);
		await Task.Delay(TimeSpan.FromMilliseconds(200));
		stub.MetadataGate.SetResult();
		await Task.WhenAll(first, second);

		await Assert.That(stub.MetadataLookups(shared.PlexId.Value)).IsEqualTo(1);
	}

	private static async Task WaitForMetadataLookups(PlexStub stub, int expected)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (stub.MetadataLookups() < expected)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
		}
	}
}
