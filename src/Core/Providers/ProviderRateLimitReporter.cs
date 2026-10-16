using System.Collections.Concurrent;
using Contracts;
using Contracts.Messaging;
using Contracts.Providers;
using Domain.Identity;
using Domain.Notifications;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Providers;

internal sealed class ProviderRateLimitReporter(IServiceScopeFactory scopeFactory, IDateTimeProvider dateTimeProvider) : IProviderRateLimitReporter
{
	private readonly ConcurrentDictionary<RemoteService, DateTimeOffset> _limitedUntil = new();

	public async Task ReportAsync(RemoteService remote, TimeSpan retryAfter, UserId triggeredBy, CancellationToken cancellationToken = default)
	{
		var now = dateTimeProvider.Now;
		if (!StartsBurst(remote, now, now + retryAfter))
		{
			return;
		}

		await using var scope = scopeFactory.CreateAsyncScope();
		var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
		await publisher.PublishAsync(new ProviderRateLimitedNotification(triggeredBy, now, remote, retryAfter), cancellationToken);
	}

	private bool StartsBurst(RemoteService remote, DateTimeOffset now, DateTimeOffset until)
	{
		var starting = true;
		_limitedUntil.AddOrUpdate(
			remote,
			until,
			(_, current) =>
			{
				starting = current <= now;
				return until;
			}
		);
		return starting;
	}
}
