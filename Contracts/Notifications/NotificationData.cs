namespace Contracts.Notifications;

public sealed record NotificationData
{
	public IReadOnlyList<NotificationMedia>? PulledAdds { get; init; }
	public IReadOnlyList<NotificationMedia>? PulledRemoves { get; init; }
	public IReadOnlyList<NotificationMedia>? PushedAdds { get; init; }
	public IReadOnlyList<NotificationMedia>? PushedRemoves { get; init; }
	public IReadOnlyList<NotificationMedia>? Media { get; init; }
	public string? FeedId { get; init; }
	public int? Added { get; init; }
	public int? Removed { get; init; }
	public string? Reason { get; init; }
}
