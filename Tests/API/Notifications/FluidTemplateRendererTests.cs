using API.Notifications;
using Contracts.Notifications;
using TestInfrastructure;

namespace API.Tests.Notifications;

public class FluidTemplateRendererTests
{
	private readonly FluidTemplateRenderer _testee = new();

	[Test]
	public async Task Renders_top_level_and_nested_media_fields_camelCase()
	{
		var context = new NotificationContext(
			Guid.NewGuid(),
			"watchlist.items.added",
			DateTimeOffset.UtcNow,
			"Watchlist items added",
			"Added.",
			new NotificationData { Media = [new NotificationMedia("id-1", "The Matrix", "Movie")] }
		);

		var result = _testee.Render("{{ title }}: {% for m in data.media %}{{ m.title }} ({{ m.type }}){% endfor %}", context);

		await Assert.That(result).IsSuccess().And.Multiple(async rendered => await Assert.That(rendered).IsEqualTo("Watchlist items added: The Matrix (Movie)"));
	}

	[Test]
	public async Task Invalid_template_returns_error()
	{
		var context = new NotificationContext(Guid.NewGuid(), "test.ping", DateTimeOffset.UtcNow, "Test", "Test.", new NotificationData());

		var result = _testee.Render("{{ unclosed", context);

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task Render_exceeding_the_step_limit_returns_an_error()
	{
		var context = new NotificationContext(Guid.NewGuid(), "test.ping", DateTimeOffset.UtcNow, "Test", "Test.", new NotificationData());

		var result = _testee.Render("{% for i in (1..100000) %}{{ i }}{% endfor %}", context);

		await Assert.That(result).IsError();
	}
}
