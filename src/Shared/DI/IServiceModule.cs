using Microsoft.Extensions.DependencyInjection;

namespace Shared.DI;

public interface IServiceModule
{
	void RegisterServices(IServiceCollection services);
}
