using System.Net;
using API.Tvmaze;
using API.Tvmaze.Types;
using Contracts.Caching;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Tvmaze;

public class TvmazeTests
{
	private static readonly ImdbId BreakingBad = ImdbId.From("tt0903747", MediaType.Show);

	[Test]
	public async Task LookUpAsync_ByAnImdbId_FindsTheShowsTvmazeAndTvdbIdsWithItsNameAsHints()
	{
		var handler = await AnsweringAsync();
		var testee = new TvmazeIdConverter(Api(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
			{
				await Assert
					.That(found.Ids)
					.IsEquivalentTo(new IExternalMediaId[] { TvmazeId.From("169", MediaType.Show), TvdbId.From("81189", MediaType.Show), BreakingBad });
				await Assert
					.That(found.Hints)
					.IsSome()
					.And.IsEqualTo(
						new IdentifyHints
						{
							Title = "Breaking Bad",
							Year = 2008,
							MediaType = MediaType.Show,
						}
					);
			});
		handler.Verify(request => request.PathMatches(@"^/lookup/shows\?imdb=tt0903747$"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ByATvdbId_AsksTvmazeByTheTvdbId()
	{
		var handler = await AnsweringAsync();
		var testee = new TvmazeIdConverter(Api(handler));

		var result = await testee.LookUpAsync(testee.RequestById(TvdbId.From("81189", MediaType.Show)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).Contains(BreakingBad));
		handler.Verify(request => request.PathMatches(@"^/lookup/shows\?thetvdb=81189$"), Times.Once);
	}

	[Test]
	public async Task LookUpAsync_ForAShowTvmazeDoesNotKnow_FindsNoIdAndNoHints()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);
		var testee = new TvmazeIdConverter(Api(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert
			.That(result)
			.IsFound()
			.And.Multiple(async found =>
			{
				await Assert.That(found.Ids).IsEmpty();
				await Assert.That(found.Hints).IsNone();
			});
	}

	[Test]
	public async Task LookUpAsync_ForAMovie_NeverCallsOut()
	{
		var handler = await AnsweringAsync();
		var testee = new TvmazeIdConverter(Api(handler));

		var result = await testee.LookUpAsync(testee.RequestById(ImdbId.From("tt0137523", MediaType.Movie)));

		await Assert.That(result).IsFound().And.Multiple(async found => await Assert.That(found.Ids).IsEmpty());
		await Assert.That(handler.Requests).IsEmpty();
	}

	[Test]
	[Arguments(HttpStatusCode.TooManyRequests)]
	[Arguments(HttpStatusCode.ServiceUnavailable)]
	public async Task LookUpAsync_WhenTvmazeIsUnavailable_SaysTheServiceIsUnavailable(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);
		var testee = new TvmazeIdConverter(Api(handler));

		var result = await testee.LookUpAsync(testee.RequestById(BreakingBad));

		await Assert.That(result).IsOfUnionType<Answer<IdLookupResult>, Unavailable>().And.IsEqualTo(new Unavailable(RemoteService.Tvmaze, new None(), true));
	}

	[Test]
	public async Task LookUpAsync_ForARecordedShow_FillsItsDetailsWithThePlainSummary()
	{
		var testee = new TvmazeEnricher(Api(await AnsweringAsync()));

		var found = await Assert.That(await testee.LookUpAsync(BreakingBad)).IsFound();

		var details = await Assert.That(found!.Details).IsTypeOf<ShowDetails>();
		await Assert.That(details!.Title).IsEqualTo("Breaking Bad");
		await Assert.That(details.Summary).StartsWith("Breaking Bad follows protagonist Walter White");
		await Assert.That(details.PosterUrl).IsEqualTo("https://static.tvmaze.com/uploads/images/original_untouched/501/1253519.jpg");
		await Assert.That(details.ReleaseDate).IsEqualTo(new DateOnly(2008, 1, 20));
		await Assert.That(details.Genres).IsEquivalentTo(["Drama", "Crime", "Thriller"]);
		await Assert.That(details.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(60));
	}

	[Test]
	public async Task Defers_TheTitleSoItNeverReplacesOneAnEnglishSourceGave()
	{
		var testee = new TvmazeEnricher(Api(Mock.HttpHandler()));

		await Assert.That(testee.Defers).IsEqualTo((MediaFields)MediaField.Title);
	}

	private static async Task<MockHttpHandler> AnsweringAsync()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Tvmaze", "Recorded", "breaking-bad.json")));
		return handler;
	}

	private static TvmazeApi Api(MockHttpHandler handler)
	{
		var cache = ITypedMemoryCache<string, TvmazeTypes.Show>.Mock();
		cache.GetValueOrNone(Any()).Returns(new None());
		return new TvmazeApi(handler.CreateClient("https://api.tvmaze.com/"), cache.Object);
	}
}
