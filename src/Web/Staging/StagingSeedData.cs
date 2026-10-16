using Domain.Feed;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;

namespace Web.Staging;

internal sealed record StagingSeedData
{
	public required IReadOnlyList<SeedTitle> Wantlist { get; init; }

	public required IReadOnlyList<SeedFeed> Feeds { get; init; }

	public required SeedLibrary Library { get; init; }

	public required SeedNotifications Notifications { get; init; }
}

internal sealed record SeedTitle
{
	public required MediaType Type { get; init; }

	public string? Tmdb { get; init; }

	public string? Tvdb { get; init; }

	internal IReadOnlyList<IExternalMediaId> Ids()
	{
		Optional<IExternalMediaId>[] ids =
		[
			Optional.Of(Tmdb).Map(id => (IExternalMediaId)TmdbId.From(id, Type)),
			Optional.Of(Tvdb).Map(id => (IExternalMediaId)TvdbId.From(id, Type)),
		];
		return [.. ids.WhereSome()];
	}
}

internal enum SeedStrategy
{
	Append,
	Mirror,
}

internal sealed record SeedFeed
{
	public required string Name { get; init; }

	public required FeedProvider Provider { get; init; }

	public required string Identifier { get; init; }

	public required SeedStrategy Strategy { get; init; }

	public int? Limit { get; init; }

	internal FeedStrategy FeedStrategy()
	{
		return Strategy switch
		{
			SeedStrategy.Append => new FeedStrategy(new Append()),
			SeedStrategy.Mirror => new FeedStrategy(new Mirror()),
		};
	}
}

internal sealed record SeedLibrary
{
	public required IReadOnlyList<LibraryTitle> Radarr { get; init; }

	public required IReadOnlyList<LibraryTitle> Sonarr { get; init; }
}

internal sealed record LibraryTitle
{
	public required int Id { get; init; }

	public bool OnDisk { get; init; }

	public string? Owner { get; init; }
}

internal sealed record SeedNotifications
{
	public required SeedTemplate Template { get; init; }

	public required SeedWebhook Webhook { get; init; }

	public required SeedPushover Pushover { get; init; }
}

internal sealed record SeedTemplate
{
	public string? Title { get; init; }

	public string? Message { get; init; }

	internal NotificationTemplate ToNotificationTemplate() => NotificationTemplate.None with { Title = Optional.Of(Title), Message = Optional.Of(Message) };
}

internal sealed record SeedWebhook
{
	public required string Name { get; init; }

	public required string Secret { get; init; }
}

internal sealed record SeedPushover
{
	public required string Name { get; init; }

	public required string AppToken { get; init; }

	public required string UserKey { get; init; }
}
