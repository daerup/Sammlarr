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
			services.AddScoped<TInterface>(provider => TracingProxy<TInterface>.Wrap(provider.GetRequiredService<TImplementation>()));
		}

		public void AddTracedKeyedScoped<TInterface, TImplementation>(object key)
			where TInterface : class
			where TImplementation : class, TInterface
		{
			services.AddKeyedScoped<TImplementation>(key);
			services.AddKeyedScoped<TInterface>(
				key,
				(provider, resolvedKey) => TracingProxy<TInterface>.Wrap(provider.GetRequiredKeyedService<TImplementation>(resolvedKey))
			);
		}
	}
}
