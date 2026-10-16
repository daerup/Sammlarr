using System.Net;
using API.Plex;
using Contracts;
using Contracts.Media;
using Domain.Media;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class PlexMatchTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly MediaSpec FightClub = new("Fight Club", MediaType.Movie, [Tmdb("550"), Imdb("tt0137523"), Plex("550")]);

	private static readonly IdentifyRequest FightClubRequest = new(
		[Tmdb("550").ToExternal(MediaType.Movie), Imdb("tt0137523").ToExternal(MediaType.Movie)],
		new None()
	);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task LoadListAsync_ForAMovieWithATmdbId_CostsOneMatchCall()
	{
		await host.PlexMatches(FightClub);
		host.PlexHolds(FightClub);

		var result = await LoadAsync();

		await Assert.That(result).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(1);
	}

	[Test]
	public async Task LoadListAsync_WhenTheMatchFindsNothing_SettlesNotFoundWithoutSearching()
	{
		await host.PlexMatchesNothing(FightClub);

		var result = await LoadAsync();

		var unmatched = await Assert.That(result).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.NotFound);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.SearchPath)).IsEqualTo(0);
	}

	[Test]
	public async Task LoadListAsync_WhenTheMatchAnswers503_SettlesCouldNotLookUp()
	{
		await host.PlexMatchFails(FightClub, HttpStatusCode.ServiceUnavailable);

		var result = await LoadAsync();

		var unmatched = await Assert.That(result).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.CouldNotLookUp);
	}

	[Test]
	public async Task LoadListAsync_AnHourAfterAMiss_AsksTheMatchAgain()
	{
		host.ExpireCachesOnTheClock();
		await host.PlexMatchesNothing(FightClub);
		await LoadAsync();
		host.SetCurrentTime(host.Resolve<IDateTimeProvider>().Now + TimeSpan.FromHours(1));

		await LoadAsync();

		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(2);
	}

	[Test]
	public async Task LoadListAsync_MinutesAfterAMiss_SettlesNotFoundEarlierWithoutAsking()
	{
		host.ExpireCachesOnTheClock();
		await host.PlexMatchesNothing(FightClub);
		await LoadAsync();
		host.SetCurrentTime(host.Resolve<IDateTimeProvider>().Now + TimeSpan.FromMinutes(59));

		var result = await LoadAsync();

		var unmatched = await Assert.That(result).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.NotFoundEarlier);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(1);
	}

	private async Task<MediaMatchResult> LoadAsync()
	{
		var results = await host.Resolve<IMediaService>()
			.LoadListAsync(new[] { FightClubRequest }.ToAsyncEnumerable().AsListTitles(), FieldNeeds.None)
			.InListOrderAsync()
			.ToListAsync();
		return results.Single();
	}
}
