using Microsoft.Extensions.DependencyInjection;

namespace Observability.Tracing;

public static class TracingRegistration
{
	extension(IServiceCollection services)
	{
		public void AddTracedScoped<TInterface, TImplementation>()
			where TInterface : class
			where TImplementation : class, TInterface
		{
			services.AddScoped<TImplementation>();
			services.AddScoped(provider => TracingProxy<TInterface>.Wrap(provider.GetRequiredService<TImplementation>()));
		}

		public void AddTracedKeyedScoped<TInterface>(object key, Func<IServiceProvider, TInterface> factory)
			where TInterface : class
		{
			services.AddKeyedScoped(key, (provider, _) => TracingProxy<TInterface>.Wrap(factory(provider)));
		}
	}
}
