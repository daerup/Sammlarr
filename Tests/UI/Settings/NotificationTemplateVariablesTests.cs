using Domain.Notifications;
using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class NotificationTemplateVariablesTests
{
	[Test]
	public async Task Variables_ForAFailedFeedRun_ShowReasonAndFeedButNotMedia()
	{
		var tokens = Tokens(NotificationEventType.FeedRunFailed);

		await Assert.That(tokens).Contains("{{ data.reason }}");
		await Assert.That(tokens).Contains("{{ data.feed.name }}");
		await Assert.That(tokens.Contains("{{ data.pulledAdds }}")).IsFalse();
		await Assert.That(tokens.Contains("{{ item.title }}")).IsFalse();
	}

	[Test]
	public async Task Variables_ForAWatchlistSync_ShowMediaItemFieldsWithDescriptor()
	{
		var tokens = Tokens(NotificationEventType.WatchlistSynced);

		await Assert.That(tokens).Contains("{{ item.title }}");
		await Assert.That(tokens).Contains("{{ item.descriptor }}");
		await Assert.That(tokens.Contains("{{ data.pulledAdds }}")).IsFalse();
		await Assert.That(tokens.Contains("{{ data.reason }}")).IsFalse();
	}

	[Test]
	public async Task Variables_ForARateLimitedProvider_ShowTheProviderAndRetryDelay()
	{
		var tokens = Tokens(NotificationEventType.ProviderRateLimited);

		await Assert.That(tokens).Contains("{{ data.provider }}");
		await Assert.That(tokens).Contains("{{ data.retryAfter }}");
		await Assert.That(tokens.Contains("{{ data.reason }}")).IsFalse();
	}

	[Test]
	public async Task Variables_ForAnyEvent_AlwaysIncludeTheTopLevelOnes()
	{
		await Assert.That(Tokens(NotificationEventType.FeedRunCompleted)).Contains("{{ title }}");
	}

	[Test]
	public async Task Variables_ForMultipleEvents_ShowTheUnionOfTheirs()
	{
		var tokens = Tokens(NotificationEventType.FeedRunFailed, NotificationEventType.WatchlistSynced);

		await Assert.That(tokens).Contains("{{ data.reason }}");
		await Assert.That(tokens).Contains("{{ item.title }}");
	}

	private static HashSet<string> Tokens(params NotificationEventType[] eventTypes) =>
		NotificationTemplateVariables.GroupsFor(eventTypes).SelectMany(group => group.Variables).Select(variable => variable.Token).ToHashSet();
}
