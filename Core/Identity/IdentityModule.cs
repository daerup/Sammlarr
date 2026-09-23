using Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Identity;

internal class IdentityModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<CurrentUserOverride>();
		services.AddScoped<IUserScope, UserScope>();
		services.AddTracedScoped<IUserManagementService, UserManagementService>();
		services.AddScoped<ISessionValidator, SessionValidator>();
	}
}
