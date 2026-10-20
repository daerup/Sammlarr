using Contracts.Feed;
using Contracts.Providers;
using Contracts.Wantlist;
using Core.Feed;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.Time.Testing;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Feed;

public class FeedServiceTests
{
	private const string Stored = "snoak/trakt-s-trending-shows";

	private static readonly FeedId Id = FeedId.From(Guid.NewGuid());

	private readonly Mock<IFeedRepository> _repository = IFeedRepository.Mock();

	private readonly Mock<IFeedProvider> _mdblist = IFeedProvider.Mock();

	private readonly Mock<IServiceReadiness> _readiness = IServiceReadiness.Mock();

	private readonly Mock<IDeletedFeeds> _deleted = IDeletedFeeds.Mock();

	private static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 10, 0, TimeSpan.Zero);

	private IFeedService Testee
	{
		get
		{
			return new FeedService(
				_repository.Object,
				IWantlistRepository.Mock().Object,
				IFeedScheduler.Mock().Object,
				new FakeKeyedServiceIndex<FeedProvider, IFeedProvider>().Register(FeedProvider.Mdblist, _mdblist.Object),
				_readiness.Object,
				_deleted.Object,
				Clock()
			);
		}
	}

	[Before(Test)]
	public void Setup()
	{
		_repository.CreateAsync(Any<FeedCreateData>(), Any()).Returns(Id);
		_repository.FindAsync(Id, Any()).Returns(Some.Of(StoredFeed()));
		_repository.GetAllAsync(Any()).Returns([StoredFeed()]);
		_repository.PauseAsync(Any(), Any()).Returns((ids, _) => [.. ids]);
	}

	[Test]
	public async Task DeleteAsync_WhenTheFeedCannotBeDeleted_KeepsNoNameForIt()
	{
		_repository.DeleteAsync(Id, Any()).Throws(new InvalidOperationException("database is locked"));

		await Assert.ThrowsAsync<InvalidOperationException>(() => Testee.DeleteAsync(Id));

		_deleted.AddAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task DeleteAsync_AFeedThatIsGone_KeepsNoName()
	{
		_repository.FindAsync(Id, Any()).Returns(new None());

		await Testee.DeleteAsync(Id);

		_deleted.AddAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task PauseFeedsOfAsync_WhenTheClearedKeyLeavesItsFeedsUnready_PausesThem()
	{
		_readiness.ReadyAsync(Any(), Any()).Returns(false);

		await Assert.That(await Testee.PauseFeedsOfAsync(RemoteService.Mdblist)).IsEquivalentTo([Id]);
	}

	[Test]
	public async Task PauseFeedsOfAsync_WhileTheServiceIsStillReady_PausesNothing()
	{
		_readiness.ReadyAsync(Any(), Any()).Returns(true);

		await Assert.That(await Testee.PauseFeedsOfAsync(RemoteService.Mdblist)).IsEmpty();
	}

	[Test]
	public async Task PauseFeedsOfAsync_ForAServiceTheFeedsDoNotNeed_LeavesThemAlone()
	{
		_readiness.ReadyAsync(Any(), Any()).Returns(false);

		await Assert.That(await Testee.PauseFeedsOfAsync(RemoteService.Tmdb)).IsEmpty();
	}

	[Test]
	public async Task CreateAsync_StoresTheFormTheProviderConvertsTo()
	{
		_mdblist.NormalizeAsync("88434", Any()).Returns(Success.Of(Stored));

		var created = await Testee.CreateAsync(CreateData("88434"));

		await Assert.That(created).IsSuccess().And.IsEqualTo(Id);
		_repository.CreateAsync(Is<FeedCreateData>(data => data!.Identifier == Stored), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task CreateAsync_AsksTheProviderAsTheFeedEditorForTheNewFeed()
	{
		UsageMark mark = default;
		_mdblist.NormalizeAsync("88434", Any()).Returns(Success.Of(Stored)).Callback(() => mark = UsageCauses.Current);

		await Testee.CreateAsync(CreateData("88434") with { Id = Some.Of(Id) });

		await Assert.That(mark).IsEqualTo(new UsageMark(UsageCause.FeedEditor, Some.Of(Id)));
	}

	[Test]
	public async Task UpdateAsync_ThatChangesTheIdentifier_AsksTheProviderAsTheFeedEditorForTheFeed()
	{
		UsageMark mark = default;
		_mdblist.NormalizeAsync(Any<string>(), Any()).Returns(Success.Of(Stored)).Callback(() => mark = UsageCauses.Current);

		await Testee.UpdateAsync(Id, UpdateData("88434"));

		await Assert.That(mark).IsEqualTo(new UsageMark(UsageCause.FeedEditor, Some.Of(Id)));
	}

	[Test]
	public async Task CreateAsync_WhenTheProviderRefusesTheIdentifier_StoresNothing()
	{
		_mdblist.NormalizeAsync("top-250-movies", Any()).Returns(Error.From("Paste the list's link from mdblist.com, its owner/list, or its number."));

		var created = await Testee.CreateAsync(CreateData("top-250-movies"));

		await Assert.That(created).IsError();
		_repository.CreateAsync(Any<FeedCreateData>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task UpdateAsync_ThatKeepsTheSourceAndTheIdentifier_AsksTheProviderNothing()
	{
		var updated = await Testee.UpdateAsync(Id, UpdateData(Stored) with { Name = "Renamed" });

		await Assert.That(updated).IsSuccess();
		_mdblist.NormalizeAsync(Any<string>(), Any()).WasNeverCalled();
		_repository.UpdateAsync(Id, Is<FeedUpdateData>(data => data!.Name == "Renamed"), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task UpdateAsync_ThatChangesTheIdentifier_StoresTheConvertedForm()
	{
		_mdblist
			.NormalizeAsync("https://mdblist.com/lists/garycrawfordgc/top-movies-of-the-week", Any())
			.Returns(Success.Of("garycrawfordgc/top-movies-of-the-week"));

		await Testee.UpdateAsync(Id, UpdateData("https://mdblist.com/lists/garycrawfordgc/top-movies-of-the-week"));

		_repository.UpdateAsync(Id, Is<FeedUpdateData>(data => data!.Identifier == "garycrawfordgc/top-movies-of-the-week"), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task UpdateAsync_WithAnIdentifierTheProviderRefuses_StoresNothing()
	{
		_mdblist.NormalizeAsync("top-250-movies", Any()).Returns(Error.From("Paste the list's link from mdblist.com, its owner/list, or its number."));

		var updated = await Testee.UpdateAsync(Id, UpdateData("top-250-movies"));

		await Assert.That(updated).IsError();
		_repository.UpdateAsync(Any<FeedId>(), Any<FeedUpdateData>(), Any()).WasNeverCalled();
	}

	private static TimeProvider Clock() => new FakeTimeProvider(Now);

	private static FeedCreateData CreateData(string identifier)
	{
		return new FeedCreateData
		{
			Name = "Trending shows",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
			Schedule = new FeedRunTrigger(new Manual()),
		};
	}

	private static FeedUpdateData UpdateData(string identifier)
	{
		return new FeedUpdateData
		{
			Name = "Trending shows",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
			Schedule = new FeedRunTrigger(new Manual()),
		};
	}

	private static Domain.Feed.Feed StoredFeed()
	{
		return new Domain.Feed.Feed
		{
			Id = Id,
			Name = "Trending shows",
			Enabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = Stored,
			CreatedAt = DateTimeOffset.UnixEpoch,
			Schedule = new FeedRunTrigger(new Manual()),
		};
	}
}
