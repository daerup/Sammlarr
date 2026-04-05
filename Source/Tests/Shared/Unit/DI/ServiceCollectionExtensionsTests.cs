using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Shared.Tests.Unit.DI;

public class ServiceCollectionExtensionsTests
{
	[Test]
	public async Task AddModule_WhenModuleRegistersService_AddsServiceToCollection()
	{
		var services = new ServiceCollection();
		services.AddModule<TestModule>();

		await using var serviceProvider = services.BuildServiceProvider();
		var registered = serviceProvider.GetRequiredService<ITestService>();

		await Assert.That(registered.Value).IsEqualTo("registered");
	}
}

file class TestModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddSingleton<ITestService>(new TestService("registered"));
}

file interface ITestService
{
	string Value { get; }
}

file record TestService(string Value) : ITestService;
