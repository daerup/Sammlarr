using API.Notifications;
using Contracts.Notifications;
using Domain.Notifications;
using TestInfrastructure;

namespace API.Tests.Notifications;

public class FluidTemplateRendererTests
{
	private readonly FluidTemplateRenderer _testee = new();

	[Test]
	public async Task Render_ForMediaFields_EmitsTopLevelAndNestedCamelCase()
	{
		var context = new NotificationContext(
			Guid.NewGuid(),
			NotificationEventType.WatchlistSynced,
			DateTimeOffset.UtcNow,
			"Watchlist synced",
			"Added.",
			new NotificationData { Media = [new NotificationMedia("id-1", "The Matrix", "Movie")] }
		);

		var result = _testee.Render("{{ title }}: {% for m in data.media %}{{ m.title }} ({{ m.type }}){% endfor %}", context);

		await Assert.That(result).IsSuccess().And.Multiple(async rendered => await Assert.That(rendered).IsEqualTo("Watchlist synced: The Matrix (Movie)"));
	}

	[Test]
	public async Task Render_WhenTheTemplateIsInvalid_ReturnsAnError()
	{
		var context = new NotificationContext(Guid.NewGuid(), NotificationEventType.TestPing, DateTimeOffset.UtcNow, "Test", "Test.", new NotificationData());

		var result = _testee.Render("{{ unclosed", context);

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task Render_WhenTheStepLimitIsExceeded_ReturnsAnError()
	{
		var context = new NotificationContext(Guid.NewGuid(), NotificationEventType.TestPing, DateTimeOffset.UtcNow, "Test", "Test.", new NotificationData());

		var result = _testee.Render("{% for i in (1..100000) %}{{ i }}{% endfor %}", context);

		await Assert.That(result).IsError();
	}
}
