using Domain.Providers;

namespace Contracts.Notifications;

public sealed record NotificationData
{
	public IReadOnlyList<NotificationMedia>? Media { get; init; }
	public NotificationFeed? Feed { get; init; }
	public int? Added { get; init; }
	public int? Removed { get; init; }
	public string? Reason { get; init; }
	public RemoteService? Provider { get; init; }
	public TimeSpan? RetryAfter { get; init; }
}
