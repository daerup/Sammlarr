using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class LinksModuleTests
{
	[Test]
	public async Task RegisterServices_GivesEveryMediaProviderALinker()
	{
		var services = new ServiceCollection();
		services.AddModule<LinksModule>();
		using var provider = services.BuildServiceProvider();

		await Assert.That(Enum.GetValues<MediaProvider>().Where(source => provider.GetKeyedService<IExternalPageLinker>(source) is null)).IsEmpty();
	}
}
