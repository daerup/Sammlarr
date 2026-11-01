using BlazorBlueprint.Components;
using Bunit;
using Contracts.Settings;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;
using UI.Shared.Services;

namespace UI.Tests.Media;

public class MediaListPageTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddScoped<Notifier>();
		Services.AddScoped<ScrollService>();
		var providers = IProviderConfigService.Mock();
		providers.GetAsync(Any()).Returns(ProviderBranding.OnPlex);
		Services.AddSingleton(providers.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Phone_ReachingTheEnd_ShowsTwentyMore()
	{
		var cut = this.RenderOnPhone<FiftyTitles>(_ => { }).Component;

		await cut.InvokeAsync(cut.Instance.ReachEndAsync);

		await Assert.That(cut.Find("output").TextContent).IsEqualTo("40");
	}

	[Test]
	public async Task Phone_AfterTheFilterChanges_StartsAgainAtTwenty()
	{
		var cut = this.RenderOnPhone<FiftyTitles>(_ => { }).Component;
		await cut.InvokeAsync(cut.Instance.ReachEndAsync);

		await cut.InvokeAsync(cut.Instance.SearchAsync);

		await Assert.That(cut.Find("output").TextContent).IsEqualTo("20");
	}

	[Test]
	public async Task Desktop_KeepsToOnePageOfTwenty()
	{
		var cut = Render<FiftyTitles>();

		await cut.InvokeAsync(cut.Instance.ReachEndAsync);

		await Assert.That(cut.Find("output").TextContent).IsEqualTo("20");
	}

	private sealed class FiftyTitles : MediaListPage
	{
		private static readonly IReadOnlyList<int> Titles = [.. Enumerable.Range(1, 50)];

		internal async Task ReachEndAsync()
		{
			await ShowMoreAsync();
			StateHasChanged();
		}

		internal async Task SearchAsync()
		{
			await ApplySearchAsync("1");
			StateHasChanged();
		}

		protected override Task LoadAsync() => Task.CompletedTask;

		protected override void ClearItems() { }

		protected override void ClearSelection() { }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenElement(0, "output");
			builder.AddContent(1, Paged(Titles).Count);
			builder.CloseElement();
		}
	}
}
