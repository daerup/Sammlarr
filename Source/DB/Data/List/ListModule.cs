using Contracts.Media.List;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.List;

internal class ListModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.AddScoped<IImportListRepository, ImportListRepository>();
}
