using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.List;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Mdblist;

public class MdblistModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<MdblistClient>();
		services.AddKeyedScoped<IApiKeyValidator, MdblistClient>(MediaProvider.Mdblist);
		services.AddKeyedScoped<IImportListService, MdblistService>(MediaListProvider.Mdblist);
	}
}

internal class MdblistService : IImportListService
{
	public Task<IReadOnlyList<ImportListItem>> GetAllImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();
}
