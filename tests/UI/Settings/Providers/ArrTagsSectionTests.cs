using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ArrTagsSectionTests : BunitContext
{
	private static readonly ArrTagUser Guest = new(Guid.NewGuid(), "Guest");

	private static readonly ArrTagUser Daerup = new(Guid.NewGuid(), "Daerup");

	private static readonly ArrTagFeed Feed = new(Guid.NewGuid(), "Top movies of the week");

	private readonly List<ArrTagScheme> _stored = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Rows_ShowTheTagEachWrites()
	{
		var cut = RenderTags([Guest]);

		await Assert
			.That(cut.FindAll(".arr-tag-chip").Select(chip => chip.TextContent))
			.IsEquivalentTo(["sammlarr-u-guest", "sammlarr-top-movies-of-the-week", "sammlarr"]);
	}

	[Test]
	public async Task PrefixAndProtectedTag_RestReadOnlyWithoutAField()
	{
		var cut = RenderTags([Guest]);

		await Assert.That(cut.Find("[data-locked=prefix]").TextContent.Trim()).IsEqualTo("sammlarr");
		await Assert.That(cut.Find("[data-locked=protection]").TextContent.Trim()).IsEqualTo("protected");
		await Assert.That(cut.FindComponents<BbInput>()).IsEmpty();
	}

	[Test]
	public async Task Prefix_WhenChangedInItsDialog_StoresItSlugged()
	{
		var cut = RenderTags([Guest]);

		await ChangeButton(cut, "Change the prefix").ClickAsync();
		await TypeAsync(cut, "Media Sync");
		await Button(cut, "Change prefix").ClickAsync();

		await Assert.That(_stored.Single().Prefix).IsEqualTo("media-sync");
	}

	[Test]
	public async Task Prefix_WhenTheDialogIsCancelled_StoresNothing()
	{
		var cut = RenderTags([Guest]);

		await ChangeButton(cut, "Change the prefix").ClickAsync();
		await TypeAsync(cut, "Media Sync");
		await Button(cut, "Cancel").ClickAsync();

		await Assert.That(_stored).IsEmpty();
		await Assert.That(cut.FindAll("[role=dialog]")).IsEmpty();
	}

	[Test]
	[Arguments("🎬")]
	[Arguments("sammlarr")]
	public async Task Prefix_WithNothingUsableOrUnchanged_CannotBeChanged(string typed)
	{
		var cut = RenderTags([Guest]);

		await ChangeButton(cut, "Change the prefix").ClickAsync();
		await TypeAsync(cut, typed);

		await Assert.That(Button(cut, "Change prefix").HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task ProtectedTag_WhenChangedInItsDialog_StoresTheNewName()
	{
		var cut = RenderTags([Guest]);

		await ChangeButton(cut, "Change the protected tag").ClickAsync();
		await TypeAsync(cut, "Keep Me");
		await Button(cut, "Change tag").ClickAsync();

		await Assert.That(_stored.Single().Protection).IsEqualTo("keep-me");
	}

	[Test]
	public async Task ProtectedTag_WithANameThatPosesAsAUserTag_CannotBeChanged()
	{
		var cut = RenderTags([Guest]);

		await ChangeButton(cut, "Change the protected tag").ClickAsync();
		await TypeAsync(cut, "u-guest");

		await Assert.That(Button(cut, "Change tag").HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task UserTag_WhileTwoUsersSync_IsLockedOnWithTheReason()
	{
		var cut = RenderTags([Guest, Daerup]);

		await Assert.That(cut.Find("button[role=switch][aria-label='Tag whose title it is']").HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Markup).Contains("Needed while 2 users sync here");
	}

	[Test]
	public async Task UserTag_OnAOneUserInstall_CanBeSwitchedOff()
	{
		var cut = RenderTags([Guest]);

		await cut.Find("button[role=switch][aria-label='Tag whose title it is']").ClickAsync();

		await Assert.That(_stored.Single().PerUser).IsFalse();
	}

	private static IElement ChangeButton(IRenderedComponent<IComponent> cut, string label) => cut.Find($"button[aria-label='{label}']");

	private static IElement Button(IRenderedComponent<IComponent> cut, string text) => cut.FindAll("button").Single(button => button.TextContent.Trim() == text);

	private static Task TypeAsync(IRenderedComponent<IComponent> cut, string text)
	{
		return cut.InvokeAsync(() => cut.FindComponents<BbInput>().Single(input => input.Instance.Id == "arr-tag-change").Instance.ValueChanged.InvokeAsync(text));
	}

	private IRenderedComponent<IComponent> RenderTags(IReadOnlyList<ArrTagUser> users)
	{
		Optional<ArrTagFeed> feed = Some.Of(Feed);
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<ArrTagsSection>(1);
			builder.AddComponentParameter(2, nameof(ArrTagsSection.Scheme), ArrTagScheme.Default);
			builder.AddComponentParameter(3, nameof(ArrTagsSection.Users), users);
			builder.AddComponentParameter(4, nameof(ArrTagsSection.Feed), feed);
			builder.AddComponentParameter(5, nameof(ArrTagsSection.SchemeChanged), EventCallback.Factory.Create<ArrTagScheme>(this, scheme => _stored.Add(scheme)));
			builder.CloseComponent();
		});
	}
}
