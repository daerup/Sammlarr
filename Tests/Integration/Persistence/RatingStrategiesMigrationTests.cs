using Contracts.Feed;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestInfrastructure;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RatingStrategiesMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeRatingStrategies = "20261003222031_RatingSources";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Applying_SetsEverySavedFeedToAverageEverySource()
	{
		var feeds = host.Resolve<IFeedRepository>();
		var id = await feeds.CreateAsync(
			new FeedCreateData
			{
				Name = "Comedy",
				IsEnabled = true,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "comedy",
				Schedule = new FeedRunTrigger(new Manual()),
			}
		);
		await MigrateAsync(BeforeRatingStrategies);

		await MigrateAsync(null);

		var feed = await Assert.That(await feeds.FindAsync(id)).IsSome();
		await Assert.That(feed!.RatingStrategy).IsSome().And.IsEqualTo(new RatingStrategy(AveragedRating.Everything));
	}

	private Task MigrateAsync(string? target) => host.Context.GetService<IMigrator>().MigrateAsync(target);
}
