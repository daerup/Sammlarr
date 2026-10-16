using Domain.Providers;

namespace Contracts.Notifications;

public sealed record NotificationFeed(string Id, string Name, FeedProvider Provider, string Identifier);
