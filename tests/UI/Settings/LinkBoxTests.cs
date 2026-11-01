using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Settings.Components.Users;

namespace UI.Tests.Settings;

public class LinkBoxTests : BunitContext
{
	private const string Link = "https://sammlarr.example/join/k7Qx";

	private BunitJSModuleInterop _share = null!;

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_share = JSInterop.SetupModule("./js/share.js");
	}

	[Test]
	public async Task Phone_WhereTheBrowserCanShare_PutsCopyAndShareUnderTheLink()
	{
		_share.Setup<bool>("canShare").SetResult(true);

		var cut = this.RenderOnPhone<LinkBox>(parameters => parameters.Add(box => box.Link, Link)).Component;

		cut.WaitForState(() => cut.FindAll("[data-testid=link-actions] button").Count == 2);
		await Assert.That(cut.FindAll("[data-testid=link-actions] button").Select(button => button.TextContent.Trim())).IsEquivalentTo(["Copy", "Share"]);
	}

	[Test]
	public async Task Phone_Share_HandsTheLinkToTheSystemShareSheet()
	{
		_share.Setup<bool>("canShare").SetResult(true);
		var cut = this.RenderOnPhone<LinkBox>(parameters => parameters.Add(box => box.Link, Link)).Component;

		cut.WaitForState(() => cut.FindAll("[data-testid=link-actions] button").Count == 2);
		await cut.Find("[data-testid=link-actions] button:last-child").ClickAsync(new());

		await Assert.That(_share.Invocations["share"].Single().Arguments.Single()).IsEqualTo(Link);
	}

	[Test]
	public async Task Phone_ShareThatFails_CopiesTheLinkInstead()
	{
		_share.Setup<bool>("canShare").SetResult(true);
		_share.Setup<bool>("share", _ => true).SetResult(false);
		var cut = this.RenderOnPhone<LinkBox>(parameters => parameters.Add(box => box.Link, Link)).Component;
		cut.WaitForState(() => cut.FindAll("[data-testid=link-actions] button").Count == 2);

		await cut.Find("[data-testid=link-actions] button:last-child").ClickAsync(new());

		await Assert.That(JSInterop.Invocations["navigator.clipboard.writeText"].Single().Arguments.Single()).IsEqualTo(Link);
		await Assert.That(cut.Find("[data-testid=link-actions] button:first-child").TextContent.Trim()).IsEqualTo("Copied");
	}

	[Test]
	public async Task Phone_WhereTheBrowserCannotShare_OffersOnlyCopy()
	{
		_share.Setup<bool>("canShare").SetResult(false);

		var cut = this.RenderOnPhone<LinkBox>(parameters => parameters.Add(box => box.Link, Link)).Component;

		cut.WaitForState(() => _share.Invocations["canShare"].Count == 1);
		await Assert.That(cut.FindAll("[data-testid=link-actions] button").Select(button => button.TextContent.Trim())).IsEquivalentTo(["Copy"]);
	}

	[Test]
	public async Task Desktop_KeepsCopyInsideTheLinkAndOffersNoShare()
	{
		var cut = Render<LinkBox>(parameters => parameters.Add(box => box.Link, Link));

		await Assert.That(cut.FindAll("[data-testid=link-actions]")).IsEmpty();
		await Assert.That(cut.FindAll("button").Select(button => button.TextContent.Trim())).IsEquivalentTo(["Copy"]);
		await Assert.That(_share.Invocations).IsEmpty();
	}
}
