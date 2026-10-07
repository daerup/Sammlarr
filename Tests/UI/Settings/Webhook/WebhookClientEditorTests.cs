using BlazorBlueprint.Components;
using Bunit;
using Domain.Notifications;
using TestInfrastructure;
using UI.Features.Settings.Components.Notifications.Webhook;

namespace UI.Tests.Settings.Webhook;

public class WebhookClientEditorTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task TryBuild_ForAnHttpUrl_BuildsTheWebhook()
	{
		var editor = Editor("http://catcher:8080/hook");

		var built = editor.TryBuild(out var config, out var error);

		await Assert.That(built).IsTrue();
		await Assert.That(error).IsNull();
		await Assert.That(config).IsOfUnionType<NotificationConfig, WebhookConfig>();
	}

	[Test]
	public async Task TryBuild_ForAnFtpUrl_RefusesAskingForHttpOrHttps()
	{
		var editor = Editor("ftp://example.test/hook");

		var built = editor.TryBuild(out _, out var error);

		await Assert.That(built).IsFalse();
		await Assert.That(error).IsEqualTo("Enter an absolute http:// or https:// URL.");
	}

	private WebhookClientEditor Editor(string url)
	{
		var target = new NotificationTarget(Guid.Empty, "Webhook", true, new WebhookConfig(url, "whsec_c2VjcmV0"));
		return Render<WebhookClientEditor>(parameters => parameters.Add(editor => editor.Target, target)).Instance;
	}
}
