using System.Net;
using System.Security.Cryptography;
using System.Text;
using API.Notifications;
using API.Notifications.Webhook;
using Contracts;
using Contracts.Notifications;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Notifications.Webhook;

public class WebhookChannelTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

	[Test]
	public async Task SendAsync_WithTheDefaultBody_PostsSignedStandardWebhooksHeaders()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);
		var context = Context();
		var secret = WebhookSigning.GenerateSecret();

		var result = await Channel(handler).SendAsync(context, WebhookTarget("https://example.test/hook", secret), Body(null), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
		await Assert.That(handler.Request!.Method).IsEqualTo(HttpMethod.Post);
		await Assert.That(Header(handler, "webhook-id")).IsEqualTo(context.Id.ToString());
		await Assert.That(Header(handler, "webhook-timestamp")).IsEqualTo("1700000000");
		await Assert.That(Header(handler, "webhook-signature")).IsEqualTo(ExpectedSignature(secret, context.Id.ToString(), "1700000000", handler.Body!));
		await Assert.That(Encoding.UTF8.GetString(handler.Body!)).Contains("\"type\":\"WatchlistSynced\"");
	}

	[Test]
	public async Task SendAsync_WithTheDefaultBody_SerialisesResolvedMediaBuckets()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);
		var context = new NotificationContext(
			Guid.NewGuid(),
			NotificationEventType.WatchlistSynced,
			Now,
			"Watchlist synced",
			"Added 1 item(s).",
			new NotificationData { Media = [new NotificationMedia("abc", "The Matrix", MediaType.Movie)] }
		);

		await Channel(handler).SendAsync(context, WebhookTarget("https://example.test/hook", WebhookSigning.GenerateSecret()), Body(null), CancellationToken.None);

		var body = Encoding.UTF8.GetString(handler.Body!);
		await Assert.That(body).Contains("\"data\":{");
		await Assert.That(body).Contains("\"media\":[{\"id\":\"abc\",\"title\":\"The Matrix\",\"type\":\"Movie\"}]");
	}

	[Test]
	public async Task SendAsync_WhenABodyTemplateIsSet_RendersItAsTheSignedBody()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		await Channel(handler)
			.SendAsync(
				Context(),
				WebhookTarget("https://example.test/hook", WebhookSigning.GenerateSecret()),
				Body("{\"t\":\"{{ title }}\"}"),
				CancellationToken.None
			);

		await Assert.That(Encoding.UTF8.GetString(handler.Body!)).IsEqualTo("{\"t\":\"Watchlist synced\"}");
	}

	[Test]
	public async Task SendAsync_WhenTheUrlIsNotHttps_IsUnrecoverableAndSendsNothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler)
			.SendAsync(Context(), WebhookTarget("http://example.test/hook", WebhookSigning.GenerateSecret()), Body(null), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(handler.Request).IsNull();
	}

	[Test]
	public async Task SendAsync_WhenTheTemplateIsInvalid_IsUnrecoverableAndSendsNothing()
	{
		var handler = new CapturingHandler(HttpStatusCode.OK);

		var result = await Channel(handler)
			.SendAsync(Context(), WebhookTarget("https://example.test/hook", WebhookSigning.GenerateSecret()), Body("{{ unclosed"), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(handler.Request).IsNull();
	}

	[Test]
	public async Task SendAsync_WhenTheServerErrors_IsRetryable()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.InternalServerError))
			.SendAsync(Context(), WebhookTarget("https://example.test/hook", WebhookSigning.GenerateSecret()), Body(null), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryRetryable>();
	}

	[Test]
	public async Task SendAsync_WhenUnauthorized_IsUnrecoverable()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.Unauthorized))
			.SendAsync(Context(), WebhookTarget("https://example.test/hook", WebhookSigning.GenerateSecret()), Body(null), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
	}

	[Test]
	public async Task SendAsync_WhenRateLimited_IsRetryable()
	{
		var result = await Channel(new CapturingHandler(HttpStatusCode.TooManyRequests))
			.SendAsync(Context(), WebhookTarget("https://example.test/hook", WebhookSigning.GenerateSecret()), Body(null), CancellationToken.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryRetryable>();
	}

	private static WebhookChannel Channel(HttpMessageHandler handler)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		return new WebhookChannel(new WebhookClient(new HttpClient(handler)), new FluidTemplateRenderer(), clock.Object, NullLogger<WebhookChannel>.Instance);
	}

	private static NotificationContext Context() =>
		new(Guid.NewGuid(), NotificationEventType.WatchlistSynced, Now, "Watchlist synced", "Synced.", new NotificationData());

	private static NotificationTarget WebhookTarget(string url, string secret) => new(Guid.NewGuid(), "Hook", true, new WebhookConfig(url, secret));

	private static NotificationTemplate Body(string? template) =>
		template is null ? NotificationTemplate.None : new NotificationTemplate(Some.Of(template), new None(), new None(), new None());

	private static string Header(CapturingHandler handler, string name) => handler.Request!.Headers.GetValues(name).Single();

	private static string ExpectedSignature(string secret, string id, string timestamp, byte[] body)
	{
		var key = Convert.FromBase64String(secret["whsec_".Length..]);
		var content = Encoding.UTF8.GetBytes($"{id}.{timestamp}.").Concat(body).ToArray();
		return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, content));
	}

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
