using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using API.Imdb;
using Contracts.Datasets;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Imdb;

public class ImdbRatingsFileTests
{
	private const string Recorded = "tconst\taverageRating\tnumVotes\ntt0137523\t8.8\t2669240\ntt0903747\t9.5\t2686170\n";

	private static readonly ImdbId FightClub = ImdbId.From("tt0137523", MediaType.Movie);

	[Test]
	public async Task DownloadAsync_ForANewFile_ReadsEveryRatingAndTheFilesETag()
	{
		var handler = Answering(Gzip(Recorded));

		var file = await DownloadAsync(handler, new None());

		await Assert.That(file.ETag).IsSome().And.IsEqualTo("\"today\"");
		await Assert
			.That(await file.Rows.ToListAsync())
			.IsEquivalentTo([new ImdbRatingLine("tt0137523", 8.8f, 2_669_240), new ImdbRatingLine("tt0903747", 9.5f, 2_686_170)]);
	}

	[Test]
	public async Task DownloadAsync_WithTheLastETag_AsksOnlyForAChangedFile()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotModified);

		var result = await new ImdbRatingsFile(handler.CreateClient("https://datasets.imdbws.com/")).DownloadAsync(Some.Of("\"yesterday\""));

		await Assert.That(result).IsSuccess().And.Multiple(async file => await Assert.That(file).IsNone());
		handler.Verify(request => request.PathMatches("^/title.ratings.tsv.gz$").Header("If-None-Match", "\"yesterday\""), Times.Once);
	}

	[Test]
	public async Task DownloadAsync_ForATruncatedFile_FailsWhileReadingIt()
	{
		var whole = Gzip(Recorded);
		var file = await DownloadAsync(Answering(whole[..(whole.Length - 12)]), new None());

		await Assert.That(async () => await file.Rows.ToListAsync()).Throws<InvalidDataException>();
	}

	[Test]
	public async Task DownloadAsync_ForAFileWithoutItsHeader_FailsWhileReadingIt()
	{
		var file = await DownloadAsync(Answering(Gzip("tt0137523\t8.8\t2669240\n")), new None());

		await Assert.That(async () => await file.Rows.ToListAsync()).Throws<InvalidDataException>();
	}

	[Test]
	public async Task DownloadAsync_ForALineItCannotRead_FailsWhileReadingIt()
	{
		var file = await DownloadAsync(Answering(Gzip("tconst\taverageRating\tnumVotes\ntt0137523\teight\t2669240\n")), new None());

		await Assert.That(async () => await file.Rows.ToListAsync()).Throws<InvalidDataException>();
	}

	[Test]
	public async Task DownloadAsync_WhenTheHostIsDown_SaysTheServiceIsUnavailable()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.ServiceUnavailable);

		var result = await new ImdbRatingsFile(handler.CreateClient("https://datasets.imdbws.com/")).DownloadAsync(new None());

		await Assert
			.That(result)
			.IsError()
			.And.Multiple(async error =>
				await Assert.That(error.CausedBy<RemoteUnavailableException>().Map(cause => cause.Remote)).IsSome().And.IsEqualTo(RemoteService.ImdbDatasets)
			);
	}

	[Test]
	public async Task DownloadAsync_WhenTheHostCannotBeReached_FailsWithWhy()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("No such host is known."));

		var result = await new ImdbRatingsFile(handler.CreateClient("https://datasets.imdbws.com/")).DownloadAsync(new None());

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).Contains("No such host is known."));
	}

	[Test]
	public async Task LookUpAsync_BeforeTheFirstLoad_IsUnavailableSoNoEmptyAnswerIsStored()
	{
		var (testee, _) = Enricher(loaded: false);

		await Assert
			.That(await testee.LookUpAsync(FightClub, []))
			.IsOfUnionType<Answer<FoundMedia>, Unavailable>()
			.And.IsEqualTo(new Unavailable(RemoteService.ImdbDatasets, new None(), false));
	}

	[Test]
	public async Task LookUpAsync_AfterALoad_RatesTheTitleWithItsVotes()
	{
		var (testee, ratings) = Enricher(loaded: true);
		ratings.FindAsync("tt0137523", Any()).Returns(Some.Of(new ImdbRating(8.8f, Some.Of(2_669_240))));

		var found = await Assert.That(await testee.LookUpAsync(FightClub, [])).IsFound();

		await Assert.That(found!.Details.Ratings.All).IsEquivalentTo(new IMediaRating[] { new ImdbRating(8.8f, Some.Of(2_669_240)) });
	}

	[Test]
	public async Task LookUpAsync_ForATitleTheFileLacks_IsAnEmptyAnswerOfItsType()
	{
		var (testee, ratings) = Enricher(loaded: true);
		ratings.FindAsync(Any(), Any()).Returns(new None());

		var found = await Assert.That(await testee.LookUpAsync(FightClub, [])).IsFound();

		await Assert.That(found!.Details.Ratings.All).IsEmpty();
		await Assert.That(found.Details.Type).IsEqualTo(MediaType.Movie);
	}

	private static (ImdbFileEnricher Testee, Mock<IImdbRatings> Ratings) Enricher(bool loaded)
	{
		var ratings = IImdbRatings.Mock();
		var loads = IDatasetLoads.Mock();
		loads
			.FindAsync(DatasetNames.ImdbRatings, Any())
			.Returns(Optional.When(loaded, () => new DatasetLoad(DatasetNames.ImdbRatings, DateTimeOffset.UnixEpoch, new None(), 2)));
		return (new ImdbFileEnricher(new ImdbRatingsInRun(ratings.Object, loads.Object)), ratings);
	}

	private static async Task<DatasetFile<ImdbRatingLine>> DownloadAsync(MockHttpHandler handler, Optional<string> etag)
	{
		var result = await new ImdbRatingsFile(handler.CreateClient("https://datasets.imdbws.com/")).DownloadAsync(etag);
		var file = await Assert.That(result).IsSuccess();
		return file.GetValueOr(() => throw new InvalidOperationException("The file was not downloaded."));
	}

	private static MockHttpHandler Answering(byte[] body)
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWith(_ =>
			{
				var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
				response.Headers.ETag = new EntityTagHeaderValue("\"today\"");
				return response;
			});
		return handler;
	}

	private static byte[] Gzip(string text)
	{
		using var compressed = new MemoryStream();
		using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal))
		{
			gzip.Write(Encoding.UTF8.GetBytes(text));
		}

		return compressed.ToArray();
	}
}
