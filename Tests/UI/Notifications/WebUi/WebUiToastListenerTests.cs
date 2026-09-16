using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Domain.Identity;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Notifications.WebUi;
using UI.Shared.Services;

namespace UI.Tests.Notifications.WebUi;

public class WebUiToastListenerTests : BunitContext
{
	private static readonly UserId User = UserId.New();

	private readonly WebUiCircuits circuits = new();

	[Before(Test)]
	public void Setup()
	{
		Services.AddLogging();
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(circuits);
	}

	[Test]
	[Arguments(NotificationLevel.Info, ToastVariant.Info)]
	[Arguments(NotificationLevel.Warning, ToastVariant.Warning)]
	[Arguments(NotificationLevel.Error, ToastVariant.Destructive)]
	public async Task Show_ForALevel_RaisesAToastWithTheMatchingVariant(NotificationLevel level, ToastVariant expected)
	{
		SignedIn();
		var cut = Render<WebUiToastListener>();

		await cut.InvokeAsync(() => circuits.Show(User, new Toast(level, "Feed run failed", "TMDB is down")));

		var toast = Toasts.Single();
		await Assert.That(toast.Variant).IsEqualTo(expected);
		await Assert.That(toast.Title).IsEqualTo("Feed run failed");
		await Assert.That(toast.Description).IsEqualTo("TMDB is down");
	}

	[Test]
	public async Task Show_WhenTheToastIsForAnotherUser_RaisesNothing()
	{
		SignedIn();
		var cut = Render<WebUiToastListener>();

		await cut.InvokeAsync(() => circuits.Show(UserId.New(), NewToast()));

		await Assert.That(Toasts).IsEmpty();
	}

	[Test]
	public async Task Show_WhenNobodyIsSignedIn_RaisesNothing()
	{
		Anonymous();
		var cut = Render<WebUiToastListener>();

		await cut.InvokeAsync(() => circuits.Show(User, NewToast()));

		await Assert.That(Toasts).IsEmpty();
	}

	[Test]
	public async Task Show_WhenTheCircuitIsGone_RaisesNothing()
	{
		SignedIn();
		Render<WebUiToastListener>();
		await DisposeComponentsAsync();

		circuits.Show(User, NewToast());

		await Assert.That(Toasts).IsEmpty();
	}

	private IReadOnlyList<ToastData> Toasts => Services.GetRequiredService<ToastService>().Toasts;

	private static Toast NewToast() => new(NotificationLevel.Info, "Watchlist synced", "Synced.");

	private void SignedIn()
	{
		var currentUser = ICurrentUser.Mock();
		currentUser.IsAuthenticated.Returns(true);
		currentUser.Id.Returns(User);
		Services.AddSingleton(currentUser.Object);
	}

	private void Anonymous()
	{
		var currentUser = ICurrentUser.Mock();
		currentUser.IsAuthenticated.Returns(false);
		Services.AddSingleton(currentUser.Object);
	}
}
