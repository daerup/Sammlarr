using API.Notifications;
using Contracts.Notifications;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using TestInfrastructure;

namespace API.Tests.Notifications;

public class FluidTemplateRendererTests
{
	private readonly FluidTemplateRenderer _testee = new();

	[Test]
	public async Task Render_ForMediaFields_EmitsTopLevelAndNestedCamelCase()
	{
		var context = Context(new NotificationData { Media = [new NotificationMedia("id-1", "The Matrix", MediaType.Movie)] });
		var template = NotificationTemplate.None with { Title = Some.Of("{{ title }}: {% for m in data.media %}{{ m.title }} ({{ m.type }}){% endfor %}") };

		var result = _testee.Render(template, context);

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async rendered => await Assert.That(rendered.Title).IsSome().And.IsEqualTo("Watchlist sync completed: The Matrix (Movie)"));
	}

	[Test]
	public async Task Render_ForEveryField_RendersEachOneAgainstTheContext()
	{
		var template = NotificationTemplate.None with { Title = Some.Of("{{ title }}"), Message = Some.Of("{{ summary }}"), Body = Some.Of("{{ type }}") };

		var result = _testee.Render(template, Context());

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async rendered =>
			{
				await Assert.That(rendered.Title).IsSome().And.IsEqualTo("Watchlist sync completed");
				await Assert.That(rendered.Message).IsSome().And.IsEqualTo("Added.");
				await Assert.That(rendered.Body).IsSome().And.IsEqualTo("WatchlistSyncCompleted");
			});
	}

	[Test]
	public async Task Render_ForARetryDelay_EmitsTheTimeSpan()
	{
		var context = Context(new NotificationData { Provider = MediaProvider.Tmdb, RetryAfter = TimeSpan.FromSeconds(30) });
		var template = NotificationTemplate.None with { Message = Some.Of("{{ data.provider }} waits {{ data.retryAfter }}") };

		var result = _testee.Render(template, context);

		await Assert.That(result).IsSuccess().And.Multiple(async rendered => await Assert.That(rendered.Message).IsSome().And.IsEqualTo("Tmdb waits 00:00:30"));
	}

	[Test]
	public async Task Render_WhenAFieldHasNoTemplate_LeavesItUnrendered()
	{
		var result = _testee.Render(NotificationTemplate.None with { Message = Some.Of("{{ summary }}") }, Context());

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async rendered =>
			{
				await Assert.That(rendered.Title).IsNone();
				await Assert.That(rendered.Message).IsSome().And.IsEqualTo("Added.");
				await Assert.That(rendered.Body).IsNone();
			});
	}

	[Test]
	public async Task Render_WhenOneFieldIsInvalid_FailsTheWholeRender()
	{
		var template = NotificationTemplate.None with { Title = Some.Of("{{ title }}"), Message = Some.Of("{{ unclosed") };

		var result = _testee.Render(template, Context());

		await Assert.That(result).IsError();
	}

	[Test]
	public async Task Render_WhenTheStepLimitIsExceeded_ReturnsAnError()
	{
		var template = NotificationTemplate.None with { Body = Some.Of("{% for i in (1..100000) %}{{ i }}{% endfor %}") };

		var result = _testee.Render(template, Context());

		await Assert.That(result).IsError();
	}

	private static NotificationContext Context(NotificationData? data = null) =>
		new(
			Guid.NewGuid(),
			NotificationEventType.WatchlistSyncCompleted,
			NotificationLevel.Success,
			DateTimeOffset.UtcNow,
			"Watchlist sync completed",
			"Added.",
			data ?? new NotificationData()
		);
}
