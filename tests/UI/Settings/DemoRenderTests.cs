using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Wantlist;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shared.Options;
using UI.Features.Settings.Components.Testing;
using UI.Features.Settings.Components.Testing.Demos;
using UI.Shared.Services;
using UI.Tests.Media;

namespace UI.Tests.Settings;

public class DemoRenderTests : BunitContext
{
	private const string OpenTooltip = "[data-portal-content][data-state=open] [role=tooltip]";

	private static readonly Dictionary<Type, object> Samples = new() { [typeof(IReadOnlySet<MediaId>)] = new HashSet<MediaId>() };

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddScoped<Notifier>();
		Services.AddScoped<ScrollService>();
		Services.AddSingleton(new AccountsOptions());
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(ArtworkStub.Sizing());
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
		Services.AddSingleton<TimeProvider>(clock);
		Services.AddSingleton(IUserManagementService.Mock().Object);
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton(IWantlistService.Mock().Object);
		Services.AddSingleton(IBlocklistService.Mock().Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	public static IEnumerable<Func<Type>> Demos()
	{
		return typeof(TestingGroup)
			.Assembly.GetTypes()
			.Where(type => type.Namespace == "UI.Features.Settings.Components.Testing.Demos" && typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
			.OrderBy(type => type.Name)
			.Select(type => (Func<Type>)(() => type));
	}

	[Test]
	[MethodDataSource(nameof(Demos))]
	public async Task Demo_Renders(Type demo)
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent(0, demo);
			foreach (var required in demo.GetProperties().Where(property => property.IsDefined(typeof(EditorRequiredAttribute), false)))
			{
				builder.AddComponentParameter(1, required.Name, Samples[required.PropertyType]);
			}

			builder.CloseComponent();
		});

		await Assert.That(cut.Markup).IsNotEmpty();
	}

	[Test]
	public async Task PreviewVerdicts_OpeningEachPopover_RendersItsTooltip()
	{
		var portal = Render<BbPortalHost>();
		var cut = Render<PreviewVerdictsDemo>();
		var triggers = cut.FindAll(".search-result-status-indicator").Count;

		for (var index = 0; index < triggers; index++)
		{
			await cut.FindAll(".search-result-status-indicator")[index].ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());
			var opened = index + 1;
			portal.WaitForState(() => portal.FindAll(OpenTooltip).Count == opened);
		}

		await Assert.That(portal.FindAll(OpenTooltip)).Count().IsEqualTo(triggers);
	}
}
