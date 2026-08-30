using System.Net;
using System.Text;
using API.Notifications;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Notifications;

public class PushoverChannelTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

	[Test]
	public async Task Default_message_posts_the_expected_form_fields_to_the_pushover_endpoint()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler).SendAsync(Context(), PushoverTarget(), CancellationToken.None);

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
	public async Task Templates_override_the_title_and_message()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context(), PushoverTarget(titleTemplate: "[{{ type }}]", messageTemplate: "{{ summary }} now"), CancellationToken.None);

		var form = Form(handler);
		await Assert.That(form["title"]).IsEqualTo("[watchlist.synced]");
		await Assert.That(form["message"]).IsEqualTo("Synced. now");
	}

	[Test]
	public async Task Feed_run_failed_maps_to_high_priority()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler).SendAsync(Context("feed.run.failed"), PushoverTarget(), CancellationToken.None);

		await Assert.That(Form(handler)["priority"]).IsEqualTo("1");
	}

	[Test]
	public async Task Monthly_limit_is_permanent()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.TooManyRequests)).SendAsync(Context(), PushoverTarget(), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
	}

	[Test]
	public async Task Bad_request_is_permanent()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.BadRequest)).SendAsync(Context(), PushoverTarget(), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
	}

	[Test]
	public async Task Server_error_is_transient()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.InternalServerError)).SendAsync(Context(), PushoverTarget(), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryTransient>();
	}

	[Test]
	public async Task Invalid_template_is_permanent_and_sends_nothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler).SendAsync(Context(), PushoverTarget(messageTemplate: "{{ unclosed"), CancellationToken.None);

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
				CancellationToken.None
			);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryPermanent>();
		await Assert.That(handler.Request).IsNull();
	}

	private static PushoverChannel Channel(HttpMessageHandler handler) =>
		new(new PushoverClient(new HttpClient(handler)), new FluidTemplateRenderer(), NullLogger<PushoverChannel>.Instance);

	private static NotificationContext Context(string type = "watchlist.synced") =>
		new(Guid.NewGuid(), type, Now, "Watchlist synced", "Synced.", new NotificationData());

	private static NotificationTarget PushoverTarget(string? titleTemplate = null, string? messageTemplate = null) =>
		new(Guid.NewGuid(), "Push", true, new PushoverConfig("app-token", "user-key", titleTemplate, messageTemplate));

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
