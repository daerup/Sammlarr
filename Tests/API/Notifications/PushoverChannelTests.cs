using System.Net;
using System.Text;
using API.Notifications;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using TestInfrastructure;

namespace API.Tests.Notifications;

public class PushoverChannelTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

	[Test]
	public async Task Posts_the_rendered_template_form_fields_to_the_pushover_endpoint()
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
	public async Task Empty_message_template_is_sent_as_an_empty_placeholder()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler).SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
		var form = Form(handler);
		await Assert.That(form["title"]).IsEqualTo(string.Empty);
		await Assert.That(form["message"]).IsEqualTo("(empty)");
	}

	[Test]
	public async Task Blank_message_template_is_sent_as_an_empty_placeholder()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(title: "{{ title }}", message: ""), CancellationToken.None);

		await Assert.That(Form(handler)["message"]).IsEqualTo("(empty)");
	}

	[Test]
	public async Task Templates_override_the_title_and_message()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(title: "[{{ type }}]", message: "{{ summary }} now"), CancellationToken.None);

		var form = Form(handler);
		await Assert.That(form["title"]).IsEqualTo("[watchlist.synced]");
		await Assert.That(form["message"]).IsEqualTo("Synced. now");
	}

	[Test]
	public async Task Feed_run_failed_maps_to_high_priority()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context("feed.run.failed"), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(Form(handler)["priority"]).IsEqualTo("1");
	}

	[Test]
	public async Task Configured_priority_overrides_the_derived_one()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(message: "hi", priority: -1), CancellationToken.None);

		await Assert.That(Form(handler)["priority"]).IsEqualTo("-1");
	}

	[Test]
	public async Task Monthly_limit_is_permanent()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.TooManyRequests))
			.SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
	}

	[Test]
	public async Task Bad_request_is_permanent()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.BadRequest))
			.SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
	}

	[Test]
	public async Task Server_error_is_transient()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.InternalServerError))
			.SendAsync(Context(), PushoverTarget(), NotificationTemplate.None, CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryTransient>();
	}

	[Test]
	public async Task Invalid_template_is_permanent_and_sends_nothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler).SendAsync(Context(), PushoverTarget(), Template(message: "{{ unclosed"), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
		await Assert.That(handler.Request).IsNull();
	}

	[Test]
	public async Task Non_pushover_target_is_permanent_and_sends_nothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler)
			.SendAsync(
				Context(),
				new NotificationTarget(Guid.NewGuid(), "Hook", true, new WebhookConfig("https://example.test/hook", "whsec_x")),
				NotificationTemplate.None,
				CancellationToken.None
			);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
		await Assert.That(handler.Request).IsNull();
	}

	private static PushoverChannel Channel(HttpMessageHandler handler) =>
		new(new PushoverClient(new HttpClient(handler)), new FluidTemplateRenderer(), NullLogger<PushoverChannel>.Instance);

	private static NotificationContext Context(string type = "watchlist.synced") =>
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
