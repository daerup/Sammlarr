using DB.Data.Identity;
using DB.Data.Jobs;
using Domain.Feed;
using Domain.Identity;
using Domain.Jobs;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class JobsTableTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Queueing_TheSameFeedTwiceForOneOwner_ViolatesTheUniqueIndex()
	{
		var feed = Guid.NewGuid().ToString();
		await StoreAsync(Queued(SammlarrTestHost.SeededAdmin.Id, feed));

		await Assert.That(async () => await StoreAsync(Queued(SammlarrTestHost.SeededAdmin.Id, feed))).Throws<DbUpdateException>();
	}

	[Test]
	public async Task Queueing_TheSameFeedForTwoOwners_IsAllowed()
	{
		var feed = Guid.NewGuid().ToString();
		var other = await AnotherUserAsync();

		await StoreAsync(Queued(SammlarrTestHost.SeededAdmin.Id, feed));
		await StoreAsync(Queued(other, feed));

		await Assert.That(await CountAsync(feed)).IsEqualTo(2);
	}

	[Test]
	public async Task Queueing_AFeedWhosePreviousJobIsDone_IsAllowedBecauseTheIndexOnlyCoversQueued()
	{
		var feed = Guid.NewGuid().ToString();
		var finished = Queued(SammlarrTestHost.SeededAdmin.Id, feed) with { State = JobState.Done };
		await StoreAsync(finished);

		await StoreAsync(Queued(SammlarrTestHost.SeededAdmin.Id, feed));

		await Assert.That(await CountAsync(feed)).IsEqualTo(2);
	}

	private static JobRow Queued(Guid owner, string key) =>
		new()
		{
			Id = Guid.NewGuid(),
			OwnerId = owner,
			Kind = JobKind.FeedRun,
			Key = key,
			Request = new RunFeed(FeedId.From(Guid.NewGuid()), new Manual()),
			State = JobState.Queued,
			RunAfter = DateTimeOffset.UnixEpoch,
			Deadline = DateTimeOffset.UnixEpoch,
		};

	private async Task StoreAsync(JobRow job)
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		context.Jobs.Add(job);
		await context.SaveChangesAsync();
	}

	private async Task<Guid> AnotherUserAsync()
	{
		var id = Guid.NewGuid();
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		context.Users.Add(
			new UserRow
			{
				Id = id,
				Provider = AuthProvider.Plex,
				ExternalUserId = id.ToString(),
				DisplayName = "Second User",
				AvatarUrl = string.Empty,
				Role = UserRole.Member,
				JoinedAt = DateTimeOffset.UnixEpoch,
			}
		);
		await context.SaveChangesAsync();
		return id;
	}

	private async Task<int> CountAsync(string key)
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context.Jobs.CountAsync(job => job.Key == key);
	}
}
