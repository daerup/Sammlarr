using Contracts;
using Contracts.Messaging;
using Core.Providers;
using Domain.Identity;
using Domain.Notifications.Events;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tests.Providers;

public class ProviderRateLimitReporterTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());

	[Test]
	public async Task ReportAsync_WhenTheProviderWasNotLimited_PublishesForTheTriggeringUser()
	{
		var (testee, publisher, _) = CreateTestee();

		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(5), Owner);

		publisher.PublishAsync(Any<ProviderRateLimitedEvent>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ReportAsync_WhenTheBurstIsStillRunning_DoesNotPublishAgain()
	{
		var (testee, publisher, clock) = CreateTestee();
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(5), Owner);

		clock.Now.Returns(Now + TimeSpan.FromSeconds(1));
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(5), Owner);

		publisher.PublishAsync(Any<ProviderRateLimitedEvent>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ReportAsync_WhenThePreviousBurstElapsed_PublishesAgain()
	{
		var (testee, publisher, clock) = CreateTestee();
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(5), Owner);

		clock.Now.Returns(Now + TimeSpan.FromSeconds(6));
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(5), Owner);

		publisher.PublishAsync(Any<ProviderRateLimitedEvent>(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ReportAsync_WhenAShorterDelayFollows_EndsTheBurstAtTheNewestDelay()
	{
		var (testee, publisher, clock) = CreateTestee();
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(60), Owner);

		clock.Now.Returns(Now + TimeSpan.FromSeconds(1));
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(2), Owner);

		clock.Now.Returns(Now + TimeSpan.FromSeconds(5));
		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(5), Owner);

		publisher.PublishAsync(Any<ProviderRateLimitedEvent>(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ReportAsync_WhenAnotherProviderIsLimited_PublishesForItSeparately()
	{
		var (testee, publisher, _) = CreateTestee();

		await testee.ReportAsync(MediaProvider.Tmdb, TimeSpan.FromSeconds(30), Owner);
		await testee.ReportAsync(MediaProvider.Plex, TimeSpan.FromSeconds(30), Owner);

		publisher.PublishAsync(Any<ProviderRateLimitedEvent>(), Any()).WasCalled(Times.Exactly(2));
	}

	private static (ProviderRateLimitReporter Testee, Mock<IMessagePublisher> Publisher, Mock<IDateTimeProvider> Clock) CreateTestee()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		var publisher = IMessagePublisher.Mock();

		var services = new ServiceCollection();
		services.AddScoped(_ => publisher.Object);
		var provider = services.BuildServiceProvider();

		return (new ProviderRateLimitReporter(provider.GetRequiredService<IServiceScopeFactory>(), clock.Object), publisher, clock);
	}
}
