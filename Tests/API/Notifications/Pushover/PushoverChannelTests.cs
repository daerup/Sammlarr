using System.Net;
using System.Text;
using API.Notifications;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using TestInfrastructure;

using API.Notifications.Pushover;

namespace API.Tests.Notifications.Pushover;

public class PushoverChannelTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

	[Test]
	public async Task SendAsync_WhenSending_PostsTheRenderedFormFieldsToPushover()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler)
			.SendAsync(Context(), PushoverTarget(), Template(title: "{{ title }}", message: "{{ summary }}"), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
		await Assert.That(handler.Request!.RequestUri!.ToString()).IsEqualTo("https://api.pushover.net/1/messages.json");
		var form = Form(handler);
		await Assert.That(form["token"]).IsEqualTo("app-token");
		await Assert.That(form["user"]).IsEqualTo("user-key");
		await Assert.That(form["title"]).IsEqualTo("Watchlist synced");
		await Assert.That(form["message"]).IsEqualTo("Synced.");
		await Assert.That(form["priority"]).IsEqualTo("0");
	}

	[Test]
	public async Task SendAsync_WhenTheMessageTemplateIsEmpty_SendsAnEmptyPlaceholder()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler).SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
		var form = Form(handler);
		await Assert.That(form["title"]).IsEqualTo(string.Empty);
		await Assert.That(form["message"]).IsEqualTo("(empty)");
	}

	[Test]
	public async Task SendAsync_WhenTheMessageTemplateIsBlank_SendsAnEmptyPlaceholder()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(title: "{{ title }}", message: ""), CancellationToken.None);

		await Assert.That(Form(handler)["message"]).IsEqualTo("(empty)");
	}

	[Test]
	public async Task SendAsync_WhenTemplatesAreSet_OverrideTheTitleAndMessage()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(title: "[{{ type }}]", message: "{{ summary }} now"), CancellationToken.None);

		var form = Form(handler);
		await Assert.That(form["title"]).IsEqualTo("[WatchlistSynced]");
		await Assert.That(form["message"]).IsEqualTo("Synced. now");
	}

	[Test]
	public async Task SendAsync_WhenAFeedRunFailed_MapsToHighPriority()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(NotificationEventType.FeedRunFailed), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(Form(handler)["priority"]).IsEqualTo("1");
	}

	[Test]
	public async Task SendAsync_WhenPriorityIsConfigured_OverridesTheDerivedOne()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(message: "hi", priority: -1), CancellationToken.None);

		await Assert.That(Form(handler)["priority"]).IsEqualTo("1");
	}

	[Test]
	public async Task SendAsync_WhenTheMonthlyLimitIsReached_IsUnrecoverable()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.TooManyRequests))
			.SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
	}

	[Test]
	public async Task SendAsync_WhenBadRequest_IsUnrecoverable()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.BadRequest))
			.SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
	}

	[Test]
	public async Task SendAsync_WhenTheServerErrors_IsRetryable()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.InternalServerError))
			.SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryRetryable>();
	}

	[Test]
	public async Task SendAsync_WhenTheTemplateIsInvalid_IsUnrecoverableAndSendsNothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(message: "{{ unclosed"), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(handler.Request).IsNull();
	}

	[Test]
	public async Task SendAsync_WhenTheTargetIsNotPushover_IsUnrecoverableAndSendsNothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler)
			.SendAsync(
				Context(),
				new NotificationTarget(Guid.NewGuid(), "Hook", true, new WebhookConfig("https://example.test/hook", "whsec_x")),
				NotificationTemplate.None,
				CancellationToken.None
			);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(handler.Request).IsNull();
	}

	private static PushoverChannel Channel(HttpMessageHandler handler) =>
		new(new PushoverClient(new HttpClient(handler)), new FluidTemplateRenderer(), NullLogger<PushoverChannel>.Instance);

	private static NotificationContext Context(NotificationEventType type = NotificationEventType.WatchlistSynced) =>
		new(Guid.NewGuid(), type, Now, "Watchlist synced", "Synced.", new NotificationData());

	private static NotificationTarget PushoverTarget() => new(Guid.NewGuid(), "Push", true, new PushoverConfig("app-token", "user-key"));

	private static NotificationTemplate Template(string? title = null, string? message = null, int? priority = null) =>
		new(new None(), Opt(title), Opt(message), priority is { } value ? Some.Of(value) : new None());

	private static Optional<string> Opt(string? value) => value is null ? new None() : Some.Of(value);

	private static Dictionary<string, string> Form(CapturingHandler handler) =>
		Encoding
			.UTF8.GetString(handler.Body!)
			.Split('&')
			.Select(pair => pair.Split('=', 2))
			.ToDictionary(parts => WebUtility.UrlDecode(parts[0]), parts => WebUtility.UrlDecode(parts[1]));

	private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
	{
		public HttpRequestMessage? Request { get; private set; }
		public byte[]? Body { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Request = request;
			Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
			return new HttpResponseMessage(status);
		}
	}
}
