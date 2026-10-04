using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Identity;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Users;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings;

public class RemovePersonDialogTests : BunitContext
{
	private static readonly User Daerup = new(UserId.New(), "Daerup", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch);

	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(_users.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Remove_UntilTheirNameIsTyped_StaysDisabled()
	{
		var cut = RenderDialog(Daerup, SinkKind.Plex);

		await TypeAsync(cut, "daeru");
		var early = RemoveButton(cut).HasAttribute("disabled");
		await TypeAsync(cut, "DAERUP");

		await Assert.That(early).IsTrue();
		await Assert.That(RemoveButton(cut).HasAttribute("disabled")).IsFalse();
	}

	[Test]
	[Arguments("Guest\u200c")]
	[Arguments("Gu\u00adest")]
	[Arguments("\u200eGuest")]
	public async Task Remove_ForANameWithAnInvisibleCharacter_TakesItTypedPlainly(string name)
	{
		var cut = RenderDialog(Daerup with { DisplayName = name }, SinkKind.Plex);

		await TypeAsync(cut, "guest");

		await Assert.That(cut.Find("label[for='remove-confirm']").TextContent).Contains("Type Guest to confirm");
		await Assert.That(RemoveButton(cut).HasAttribute("disabled")).IsFalse();
	}

	[Test]
	public async Task Dialog_UnderRadarrAndSonarr_SaysWhichTitlesGoAndWhichStay()
	{
		_users.SinkHoldingsAsync(Any(), Any()).Returns(Success.Of(new SinkHoldings(12, 3, "sammlarr-u-daerup")));

		var cut = RenderDialog(Daerup, SinkKind.Arr);

		await Assert
			.That(cut.WaitForElement("[data-holdings]").TextContent.Trim())
			.IsEqualTo("12 titles carry only their tag (sammlarr-u-daerup) and go; their files stay on disk. 3 titles that someone else also wants stay.");
	}

	[Test]
	public async Task Dialog_UnderPlex_LeavesTheirWatchlistAlone()
	{
		var cut = RenderDialog(Daerup, SinkKind.Plex);

		await Assert.That(cut.Markup).Contains("Titles already on their Plex watchlist stay there.");
		_users.SinkHoldingsAsync(Any(), Any()).WasCalled(Times.Never);
	}

	private IRenderedComponent<IComponent> RenderDialog(User person, SinkKind sink)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<RemovePersonDialog>(1);
			builder.AddComponentParameter(2, nameof(RemovePersonDialog.Open), true);
			builder.AddComponentParameter(3, nameof(RemovePersonDialog.OpenChanged), EventCallback.Factory.Create<bool>(this, () => { }));
			builder.AddComponentParameter(
				4,
				nameof(RemovePersonDialog.Person),
				new JoinedPerson(person, new PersonHoldings(2, 40, 1, [MediaProvider.Tmdb]), new None(), new StoredColour(Some.Of(40d), new None()))
			);
			builder.AddComponentParameter(5, nameof(RemovePersonDialog.Sink), sink);
			builder.CloseComponent();
		});
	}

	private static Task TypeAsync(IRenderedComponent<IComponent> cut, string text)
	{
		return cut.InvokeAsync(() => cut.FindComponents<BbInput>().Single(input => input.Instance.Id == "remove-confirm").Instance.ValueChanged.InvokeAsync(text));
	}

	private static IElement RemoveButton(IRenderedComponent<IComponent> cut)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Trim().StartsWith("Remove ", StringComparison.Ordinal));
	}
}
