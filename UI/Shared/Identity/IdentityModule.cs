using Contracts.Identity;
using Shared.DI;

namespace UI.Shared.Identity;

internal class IdentityModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<CurrentUserAccessor>();
		services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserAccessor>());
		services.AddSingleton<CircuitUserRegistry>();
		services.AddSingleton<ISessionInvalidator>(sp => sp.GetRequiredService<CircuitUserRegistry>());
	}
}
