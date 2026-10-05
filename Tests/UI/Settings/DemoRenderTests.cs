using BlazorBlueprint.Components;
using Bunit;
using Contracts;
using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Ratings;
using Contracts.Wantlist;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Testing;
using UI.Shared.Services;
using UI.Shared.Services.Branding;
using UI.Tests.Media;

namespace UI.Tests.Settings;

public class DemoRenderTests : BunitContext
{
	private static readonly Dictionary<Type, object> Samples = new() { [typeof(IReadOnlySet<MediaId>)] = new HashSet<MediaId>() };

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddScoped<Notifier>();
		Services.AddScoped<ScrollService>();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(ArtworkStub.Sizing());
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
		Services.AddSingleton(clock.Object);
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
}
