using System.IO.Compression;
using System.Net;
using System.Text;
using Contracts.Datasets;
using Contracts.Media;
using Contracts.Ratings;
using Core.Datasets;
using Domain.Media;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Datasets;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ImdbRatingsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly ImdbRatingLine[] Yesterday = [new("tt0137523", 8.8f, 2_669_240), new("tt0903747", 9.5f, 2_686_170)];

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task ReplaceAsync_ThenFindAsync_FindsEachRatingWithItsVotes()
	{
		var ratings = host.Resolve<IImdbRatings>();

		var replaced = await ratings.ReplaceAsync(Yesterday.ToAsyncEnumerable());

		await Assert.That(replaced).IsEqualTo(2);
		await Assert.That(await ratings.FindAsync("tt0903747")).IsSome().And.IsEqualTo(new ImdbRating(9.5f, Some.Of(2_686_170)));
		await Assert.That(await ratings.FindAsync("tt0000001")).IsNone();
	}

	[Test]
	public async Task ReplaceAsync_WhenTheRowsFailHalfway_LeavesTheLastTable()
	{
		var ratings = host.Resolve<IImdbRatings>();
		await ratings.ReplaceAsync(Yesterday.ToAsyncEnumerable());

		await Assert.That(async () => await ratings.ReplaceAsync(CutShort())).Throws<InvalidDataException>();

		await Assert.That(await ratings.FindAsync("tt0137523")).IsSome();
		await Assert.That(await ratings.FindAsync("tt9999999")).IsNone();
	}

	[Test]
	public async Task ReplaceAsync_ForAFileWithNoRatings_LeavesTheLastTable()
	{
		var ratings = host.Resolve<IImdbRatings>();
		await ratings.ReplaceAsync(Yesterday.ToAsyncEnumerable());

		await Assert.That(async () => await ratings.ReplaceAsync(AsyncEnumerable.Empty<ImdbRatingLine>())).Throws<InvalidDataException>();

		await Assert.That(await ratings.FindAsync("tt0137523")).IsSome();
	}

	[Test]
	public async Task RecordAsync_ForASecondLoad_KeepsOnlyTheLatest()
	{
		var loads = host.Resolve<IDatasetLoads>();
		await loads.RecordAsync(new DatasetLoad(DatasetNames.ImdbRatings, DateTimeOffset.UnixEpoch, Some.Of("\"first\""), 1));
		var latest = new DatasetLoad(DatasetNames.ImdbRatings, DateTimeOffset.UnixEpoch.AddDays(1), new None(), 2);

		await loads.RecordAsync(latest);

		await Assert.That(await loads.FindAsync(DatasetNames.ImdbRatings)).IsSome().And.IsEqualTo(latest);
	}

	[Test]
	public async Task GetAvailableAsync_WithNothingConnected_OffersImdb()
	{
		await Assert.That(await host.Resolve<IRatingService>().GetAvailableAsync()).Contains(RatingSource.Imdb);
	}

	[Test]
	public async Task GetDetailsAsync_BeforeAndAfterTheFirstLoad_RatesWithImdbsVotesOnlyOnceLoaded()
	{
		host.HttpStub.RespondForUrl(
			$"{TestUrls.ImdbDatasets}/title.ratings.tsv.gz",
			_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(RatingsFile()) }
		);
		var fightClub = await host.SeedMediaAsync(ImdbId.From("tt0137523", MediaType.Movie));
		var media = host.Resolve<IMediaService>();

		var before = await media.GetDetailsAsync([fightClub]).ToListAsync();
		var loaded = await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.ImdbRatings).LoadAsync();
		host.SetCurrentTime(new DateTimeOffset(2024, 6, 1, 2, 0, 0, TimeSpan.Zero));
		var after = await media.GetDetailsAsync([fightClub]).ToListAsync();

		await Assert.That(loaded).IsSuccess().And.IsEqualTo(2);
		await Assert.That(Imdb(before.Single())).IsNone();
		await Assert.That(Imdb(after.Single())).IsSome().And.IsEqualTo<IMediaRating>(new ImdbRating(8.8f, Some.Of(2_669_240)));
	}

	private static Optional<IMediaRating> Imdb(Result<MatchedMedia> matched)
	{
		return matched.ToOptional().Map(found => found.Details.Ratings.All.FirstOrNone(rating => rating.Source == RatingSource.Imdb));
	}

	private static async IAsyncEnumerable<ImdbRatingLine> CutShort()
	{
		yield return new ImdbRatingLine("tt9999999", 7f, 10);
		await Task.Yield();
		throw new InvalidDataException("The file is cut short.");
	}

	private static byte[] RatingsFile()
	{
		using var compressed = new MemoryStream();
		using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal))
		{
			gzip.Write(Encoding.UTF8.GetBytes("tconst\taverageRating\tnumVotes\ntt0137523\t8.8\t2669240\ntt0903747\t9.5\t2686170\n"));
		}

		return compressed.ToArray();
	}
}
