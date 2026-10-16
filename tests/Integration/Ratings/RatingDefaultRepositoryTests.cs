using Contracts.Ratings;
using Domain.Identity;
using Domain.Media;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Ratings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RatingDefaultRepositoryTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IRatingDefaultRepository Testee => host.Resolve<IRatingDefaultRepository>();

	[Test]
	public async Task FindAsync_WhenNothingIsStored_ReturnsNone()
	{
		await Assert.That(await Testee.FindAsync()).IsNone();
	}

	[Test]
	public async Task StoreAsync_WhenStoredTwice_KeepsTheLatestStrategy()
	{
		var latest = new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics]));
		await Testee.StoreAsync(new RatingStrategy(new OrderedRating([RatingSource.Imdb])));

		await Testee.StoreAsync(latest);

		await Assert.That(await Testee.FindAsync()).IsSome().And.IsEqualTo(latest);
	}

	[Test]
	public async Task StoreAsync_WhenFirstSavesRace_KeepsOneOfThem()
	{
		RatingStrategy[] strategies =
		[
			.. Enum.GetValues<RatingSource>().Select(source => new RatingStrategy(new OrderedRating([source]))),
			new(AveragedRating.Everything),
		];

		await Task.WhenAll(strategies.Concat(strategies).Select(strategy => Testee.StoreAsync(strategy)));

		await Assert.That(await Testee.FindAsync()).IsSome().And.IsIn(strategies);
	}

	[Test]
	public async Task StoreAsync_ForAnotherUser_LeavesTheFirstUsersStrategy()
	{
		var mine = new RatingStrategy(new OrderedRating([RatingSource.Imdb]));
		await Testee.StoreAsync(mine);
		host.CurrentUser.Id = await host.SeedExistingUser("member-1", UserRole.Member, "Bob");
		await Testee.StoreAsync(new RatingStrategy(AveragedRating.Everything));
		await Testee.StoreAsync(new RatingStrategy(new OrderedRating([RatingSource.Tmdb])));

		host.CurrentUser.Id = TestCurrentUser.DefaultId;

		await Assert.That(await Testee.FindAsync()).IsSome().And.IsEqualTo(mine);
	}

	[Test]
	public async Task FindAsync_ForAnotherUser_DoesNotSeeTheStoredStrategy()
	{
		await Testee.StoreAsync(new RatingStrategy(new OrderedRating([RatingSource.Imdb])));

		host.CurrentUser.Id = UserId.From(Guid.NewGuid());

		await Assert.That(await Testee.FindAsync()).IsNone();
	}
}
