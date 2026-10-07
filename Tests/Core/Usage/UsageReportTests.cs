using Contracts;
using Contracts.Usage;
using Core.Usage;
using Domain.Identity;
using Domain.Providers;
using Domain.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Usage;

public class UsageReportTests
{
	private static readonly DateTimeOffset Midnight = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

	private static readonly DateTimeOffset Now = Midnight.AddHours(12.5);

	private readonly Mock<IUsageStore> _store = IUsageStore.Mock();

	private readonly Mock<IUsageLastCalls> _lastCalls = IUsageLastCalls.Mock();

	public UsageReportTests()
	{
		_lastCalls.Of(Any()).Returns(new None());
	}

	[Test]
	public async Task ReadAsync_ADay_CountsTheCallsOfEachHourAndOfTheDayBefore()
	{
		Stored(
			Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 5),
			Calls(RemoteService.Tmdb, 12, UsageOutcome.Failed, 2),
			Calls(RemoteService.Tmdb, -20, UsageOutcome.Answered, 3)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		await Assert
			.That(report.Current.Slots.Select(slot => slot.Tally.Total))
			.IsEquivalentTo([.. Enumerable.Repeat(0L, 22), 5L, 2L], CollectionOrdering.Matching);
		await Assert.That(report.Current.Tally.Errors).IsEqualTo(2);
		await Assert.That(report.Previous.Tally.Total).IsEqualTo(3);
	}

	[Test]
	public async Task ReadAsync_ManyCauses_ListsTheBusiestFirst()
	{
		Stored(
			Calls(RemoteService.Tmdb, 11, UsageOutcome.Answered, 1, UsageCause.Search),
			Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 7),
			Calls(RemoteService.Tmdb, 10, UsageOutcome.Answered, 2)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		await Assert
			.That(report.Current.Causes.Select(line => (line.Cause, line.Tally.Total)))
			.IsEquivalentTo([(UsageCause.FeedRun, 9L), (UsageCause.Search, 1L)], CollectionOrdering.Matching);
		await Assert
			.That(report.Current.Services.Select(line => line.Service))
			.IsEquivalentTo([RemoteService.Plex, RemoteService.Tmdb], CollectionOrdering.Matching);
	}

	[Test]
	public async Task ReadAsync_AServiceThatStoppedAnswering_SaysSinceWhichHour()
	{
		Stored(
			Calls(RemoteService.Plex, 9, UsageOutcome.Answered, 4),
			Calls(RemoteService.Plex, 10, UsageOutcome.Failed, 3),
			Calls(RemoteService.Plex, 11, UsageOutcome.GaveUpOn429, 2),
			Calls(RemoteService.Plex, 11, UsageOutcome.Cancelled, 1)
		);

		var plex = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.Single();

		await Assert.That(plex.FailingSince).IsSome().And.IsEqualTo(Midnight.AddHours(10));
	}

	[Test]
	public async Task ReadAsync_AServiceAnsweringAgain_IsNotFailing()
	{
		Stored(Calls(RemoteService.Plex, 10, UsageOutcome.Failed, 3), Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 1));

		var plex = (await Testee().ReadAsync(UsageRange.Day)).Current.Services.Single();

		await Assert.That(plex.FailingSince).IsNone();
	}

	[Test]
	public async Task ReadAsync_TheLastCall_PrefersTheMinuteKeptSinceTheStart()
	{
		_lastCalls.Of(RemoteService.Plex).Returns(Some.Of(Now.AddMinutes(-9)));
		Stored(
			Calls(RemoteService.Plex, 11, UsageOutcome.Answered, 1),
			Calls(RemoteService.Tmdb, 9, UsageOutcome.Answered, 1),
			Calls(RemoteService.Plex, -20, UsageOutcome.Answered, 1)
		);

		var report = await Testee().ReadAsync(UsageRange.Day);

		var services = report.Current.Services;
		await Assert.That(services.Single(line => line.Service == RemoteService.Plex).LastCall).IsSome().And.IsEqualTo(Now.AddMinutes(-9));
		await Assert.That(services.Single(line => line.Service == RemoteService.Tmdb).LastCall).IsSome().And.IsEqualTo(Midnight.AddHours(9));
		await Assert.That(report.Previous.Services.Single().LastCall).IsSome().And.IsEqualTo(Midnight.AddHours(-20));
	}

	[Test]
	public async Task ReadAsync_AsAMember_IsRefused()
	{
		await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Testee(UserRole.Member).ReadAsync(UsageRange.Day));
	}

	private void Stored(params UsageCalls[] calls)
	{
		_store.ReadAsync(Any(), Any(), Any()).Returns(new UsageRows(calls, []));
	}

	private static UsageCalls Calls(RemoteService service, int hour, UsageOutcome outcome, long calls, UsageCause cause = UsageCause.FeedRun)
	{
		return new(service, Midnight.AddHours(hour), cause, outcome, new None(), calls);
	}

	private UsageReport Testee(UserRole role = UserRole.Admin)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		return new UsageReport(_store.Object, _lastCalls.Object, clock.Object, new TestCurrentUser { Role = role });
	}
}
