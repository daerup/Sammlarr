using BlazorBlueprint.Components;
using Bunit;
using Domain.Wantlist.Arr;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ArrTagsSectionTests : BunitContext
{
	private static readonly ArrTagUser Guest = new(Guid.NewGuid(), "Guest");

	private static readonly ArrTagUser Daerup = new(Guid.NewGuid(), "Daerup");

	private readonly List<ArrTagScheme> _stored = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Prefix_WhenChanged_AsksBeforeStoringIt()
	{
		var cut = RenderTags([Guest]);

		await TypePrefixAsync(cut, "Media Sync");
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Save").ClickAsync();

		await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Change the prefix from sammlarr to media-sync?");
		await Assert.That(_stored).IsEmpty();
	}

	[Test]
	public async Task Prefix_WhenTheChangeIsConfirmed_StoresItSlugged()
	{
		var cut = RenderTags([Guest]);

		await TypePrefixAsync(cut, "Media Sync");
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Save").ClickAsync();
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Change prefix").ClickAsync();

		await Assert.That(_stored.Single().Prefix).IsEqualTo("media-sync");
	}

	[Test]
	public async Task Prefix_WhenTheChangeIsCancelled_GoesBackToTheSavedOne()
	{
		var cut = RenderTags([Guest]);

		await TypePrefixAsync(cut, "Media Sync");
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Save").ClickAsync();
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync();

		await Assert.That(cut.FindComponent<BbInput>().Instance.Value).IsEqualTo("sammlarr");
		await Assert.That(_stored).IsEmpty();
	}

	[Test]
	public async Task Prefix_WithNothingUsable_CannotBeSaved()
	{
		var cut = RenderTags([Guest]);

		await TypePrefixAsync(cut, "🎬");

		await Assert.That(cut.FindAll("button").Single(button => button.TextContent.Trim() == "Save").HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task UserTag_WhileTwoUsersSync_IsLockedOnWithTheReason()
	{
		var cut = RenderTags([Guest, Daerup]);

		await Assert.That(cut.Find("button[role=checkbox]").HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Markup).Contains("Needed while 2 users sync here");
	}

	[Test]
	public async Task UserTag_OnAOneUserInstall_CanBeSwitchedOff()
	{
		var cut = RenderTags([Guest]);

		await cut.Find("button[role=checkbox]").ClickAsync();

		await Assert.That(_stored.Single().PerUser).IsFalse();
	}

	[Test]
	public async Task Preview_ShowsTheTagsTheViewersTitleWouldGet()
	{
		var cut = RenderTags([Guest]);

		await Assert.That(cut.FindAll(".arr-tags-chip").Select(chip => chip.TextContent)).IsEquivalentTo(["sammlarr-u-guest"]);
	}

	private static Task TypePrefixAsync(IRenderedComponent<ArrTagsSection> cut, string prefix)
	{
		return cut.InvokeAsync(() => cut.FindComponent<BbInput>().Instance.ValueChanged.InvokeAsync(prefix));
	}

	private IRenderedComponent<ArrTagsSection> RenderTags(IReadOnlyList<ArrTagUser> users)
	{
		return Render<ArrTagsSection>(parameters =>
			parameters.Add(tags => tags.Scheme, ArrTagScheme.Default).Add(tags => tags.Users, users).Add(tags => tags.SchemeChanged, scheme => _stored.Add(scheme))
		);
	}
}
